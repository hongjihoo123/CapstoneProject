using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.CaptureTool
{
    // 캡처 진행 담당. EditorApplication.update 로 한 장씩 처리한다.
    //  - 씬을 저장하거나 수정하지 않는다 (열기만 함). 저장 안 된 씬이 있으면 씬을 바꾸지 않고 멈춘다.
    //  - 플레이 모드 진입 때 도메인이 다시 로드되므로 진행 상태는 SessionState 에 둔다.
    //  - 실행 중인 에디터에 신호 주기: Captures/_request.txt 에 설정 파일 경로를 쓰면 1초 안에 집어서 실행한다.
    //  - 결과는 콘솔과 Captures/_result.txt 에 "OK <경로>" / "ERR <이유>" 로 남긴다.
    [InitializeOnLoad]
    internal static class CaptureRunner
    {
        public const string DefaultOutDir = "Captures";
        public const string RequestFile = DefaultOutDir + "/_request.txt";
        public const string ResultFile = DefaultOutDir + "/_result.txt";
        public const string LastConfigPref = "HJH.Capture.LastConfig";

        private const string KPhase = "HJH.Capture.Phase", KConfig = "HJH.Capture.Config", KResults = "HJH.Capture.Results",
            KOrig = "HJH.Capture.OrigScenes", KDeadline = "HJH.Capture.Deadline", KOwnPlay = "HJH.Capture.OwnPlay", KStart = "HJH.Capture.Start";

        // 한 번의 도메인 안에서만 쓰는 값
        private static CaptureConfig _cfg;
        private static bool _shooting, _timedOut, _prevAsync;
        private static int _idx, _stage, _frames;
        private static double _stageStart, _nextPoll;
        private static string _pendingPath; private static DateTime _pendingStamp;

        private static string Phase { get => SessionState.GetString(KPhase, ""); set => SessionState.SetString(KPhase, value); }
        private static double Now => EditorApplication.timeSinceStartup;

        static CaptureRunner() { EditorApplication.update += Tick; }

        // ───────────────────────── 시작 ─────────────────────────
        public static void Start(string configPath, bool interactive)
        {
            if (Phase != "") { Debug.LogWarning("[Capture] 이미 캡처 중입니다."); return; }
            SessionState.SetString(KResults, ""); SessionState.SetString(KOrig, ""); SessionState.SetBool(KOwnPlay, false);
            SessionState.SetFloat(KStart, (float)Now);
            if (!TryLoad(configPath, out _cfg, out string full, out string err)) { Add("ERR " + err); Finish(); return; }
            SessionState.SetString(KConfig, full);
            EditorPrefs.SetString(LastConfigPref, full);
            SessionState.SetFloat(KDeadline, (float)(Now + (_cfg.maxSeconds > 0f ? _cfg.maxSeconds : 120f)));

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // 플레이 중에는 보기만 하는 play 설정만 허용 (씬을 열거나 바꾸지 않음)
                if (!EditorApplication.isPlaying || !_cfg.play) { Add("ERR 플레이 중에는 \"play\": true 설정만 찍을 수 있습니다."); Finish(); return; }
                BeginShots();
                return;
            }
            if (EditorApplication.isCompiling) { Add("ERR 스크립트 컴파일 중입니다. 끝난 뒤 다시 실행하세요."); Finish(); return; }

            if (!string.IsNullOrEmpty(_cfg.scene) && SceneManager.GetActiveScene().path != _cfg.scene)
            {
                if (!File.Exists(_cfg.scene)) { Add("ERR 씬 파일 없음: " + _cfg.scene); Finish(); return; }
                if (AnyDirty() && (!interactive || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()))
                { Add("ERR 저장 안 된 씬이 있어 씬을 바꾸지 않았습니다. 저장한 뒤 다시 실행하세요."); Finish(); return; }
                var open = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(sc => !string.IsNullOrEmpty(sc.path)).ToList();
                Scene active = SceneManager.GetActiveScene();
                SessionState.SetString(KOrig, string.Join("|", open.OrderByDescending(sc => sc == active).Select(sc => sc.path)));
                EditorSceneManager.OpenScene(_cfg.scene, OpenSceneMode.Single);
            }

            if (_cfg.play)
            {
                SessionState.SetBool(KOwnPlay, true);
                Phase = "enterPlay";
                EditorApplication.EnterPlaymode();
                return;
            }
            BeginShots();
        }

        private static bool TryLoad(string configPath, out CaptureConfig cfg, out string full, out string err)
        {
            cfg = null; err = null; full = configPath;
            if (string.IsNullOrEmpty(configPath)) { err = "설정 파일 경로가 비어 있음"; return false; }
            full = Path.GetFullPath(configPath).Replace('\\', '/');   // 상대 경로는 프로젝트 루트 기준
            if (!File.Exists(full)) { err = "설정 파일 없음: " + full; return false; }
            try { cfg = JsonUtility.FromJson<CaptureConfig>(File.ReadAllText(full)); }
            catch (Exception e) { err = "설정 파일 JSON 오류: " + e.Message; return false; }
            if (cfg == null || cfg.shots == null || cfg.shots.Length == 0) { err = "설정 파일에 shots 가 없음: " + full; return false; }
            return true;
        }

        private static bool AnyDirty()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) return true;
            return false;
        }

        // ───────────────────────── 매 프레임 ─────────────────────────
        private static void Tick()
        {
            string phase = Phase;
            if (phase == "") { PollRequest(); return; }

            if (Now > SessionState.GetFloat(KDeadline, float.MaxValue))
            {
                Add("ERR 제한 시간(maxSeconds) 초과로 중단");
                if (phase == "exitPlay") Finish(); else Abort();
                return;
            }
            switch (phase)
            {
                case "enterPlay":
                    if (EditorApplication.isPlaying)
                    {
                        if (!TryLoad(SessionState.GetString(KConfig, ""), out _cfg, out _, out string err)) { Add("ERR " + err); Abort(); return; }
                        BeginShots();
                    }
                    else if (!EditorApplication.isPlayingOrWillChangePlaymode && Now - SessionState.GetFloat(KStart, 0f) > 10f)
                    { Add("ERR 플레이 모드에 들어가지 못했습니다 (컴파일 오류가 있는지 확인)."); Finish(); }
                    break;
                case "shooting":
                    if (!_shooting) { Add("ERR 캡처가 중간에 끊겼습니다 (스크립트 재컴파일 등)."); Abort(); return; }
                    if (_cfg.play && !EditorApplication.isPlaying) { Add("ERR 플레이가 먼저 끝나 중단"); EndShots(); return; }
                    Step();
                    break;
                case "exitPlay":
                    if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) Finish();
                    break;
            }
        }

        private static void PollRequest()
        {
            if (Now < _nextPoll) return;
            _nextPoll = Now + 1.0;
            if (EditorApplication.isCompiling || !File.Exists(RequestFile)) return;
            string config;
            try { config = File.ReadAllText(RequestFile).Trim(); File.Delete(RequestFile); }   // 신호 파일은 도구가 만든 임시 파일
            catch (IOException) { return; }   // 아직 쓰는 중이면 다음에
            if (config == "refresh") { WriteResult("OK refresh\n"); AssetDatabase.Refresh(); return; }   // 에디터가 뒤에 있어도 새 스크립트를 컴파일하게
            if (config.StartsWith("menu:")) { RunMenu(config.Substring(5).Trim()); return; }
            Start(config, false);
        }

        // HJH/ 메뉴 하나를 대신 눌러 준다 (플레이 중·컴파일 오류 중에는 안 함). 캡처→수정→캡처를 사람 손 없이 돌리기 위한 최소 기능.
        private static void RunMenu(string menu)
        {
            string line;
            if (!menu.StartsWith("HJH/")) line = "ERR HJH/ 메뉴만 실행할 수 있습니다: " + menu;
            else if (EditorApplication.isPlayingOrWillChangePlaymode) line = "ERR 플레이 중에는 메뉴를 실행하지 않습니다.";
            else if (EditorUtility.scriptCompilationFailed) line = "ERR 스크립트 컴파일 오류가 있습니다 (콘솔 확인).";
            else
            {
                try { line = EditorApplication.ExecuteMenuItem(menu) ? "OK menu " + menu : "ERR 메뉴를 찾지 못했거나 비활성: " + menu; }
                catch (Exception e) { line = "ERR 메뉴 실행 중 예외: " + e.Message; }
            }
            Debug.Log("[Capture] " + line);
            WriteResult(line + "\n");
        }

        private static void WriteResult(string text)
        {
            try { Directory.CreateDirectory(DefaultOutDir); File.WriteAllText(ResultFile, text + "DONE\n"); }
            catch (IOException e) { Debug.LogWarning("[Capture] 결과 파일을 못 씀: " + e.Message); }
        }

        // ───────────────────────── 촬영 ─────────────────────────
        private static void BeginShots()
        {
            _idx = 0; _stage = 0; _shooting = true;
            _prevAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;   // 컴파일 덜 된 셰이더가 하늘색으로 찍히지 않게
            if (EditorApplication.isPlaying) Application.runInBackground = true;   // 에디터가 뒤에 있어도 멈추지 않게 (플레이 끝나면 원래대로)
            Phase = "shooting";
        }

        private static string Mode(CaptureShot s) => string.IsNullOrEmpty(s.mode) ? "free" : s.mode;
        private static int W => _cfg.width > 0 ? _cfg.width : 1280;
        private static int H => _cfg.height > 0 ? _cfg.height : 720;
        private static string OutDir => string.IsNullOrEmpty(_cfg.outDir) ? DefaultOutDir : _cfg.outDir;

        private static void Step()
        {
            if (_idx >= _cfg.shots.Length) { EndShots(); return; }
            CaptureShot s = _cfg.shots[_idx];
            bool playing = EditorApplication.isPlaying;
            switch (_stage)
            {
                case 0:   // 시간 조건
                    if (playing && Time.time < s.delay) return;
                    if (playing && !string.IsNullOrEmpty(s.forceState) && !CaptureRenderer.ForceState(s.target, s.forceState))
                        Add($"WARN {s.name}: forceState '{s.forceState}' 를 가진 Animator 를 못 찾음");
                    _stageStart = Now; _timedOut = false; _stage = 1;
                    return;
                case 1:   // 상태 조건
                    if (playing && !string.IsNullOrEmpty(s.waitState) && !CaptureRenderer.InState(s.target, s.waitState))
                    {
                        if (Now - _stageStart < (s.timeout > 0f ? s.timeout : 10f)) return;
                        _timedOut = true;
                    }
                    if (Mode(s) == "scene")
                    {
                        string err = AlignSceneView(s);
                        if (err != null) { Add($"ERR {s.name}: {err}"); Next(); return; }
                    }
                    _frames = 3; _stage = 2;
                    return;
                case 2:   // 몇 프레임 기다린 뒤 찍기
                    if (Mode(s) == "scene" && SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
                    if (--_frames > 0) return;
                    Shoot(s, playing);
                    return;
                case 3:   // ScreenCapture 는 프레임 끝에 파일을 쓰므로 생길 때까지 기다림
                    if (File.Exists(_pendingPath) && File.GetLastWriteTimeUtc(_pendingPath) > _pendingStamp && new FileInfo(_pendingPath).Length > 0)
                    { Add("OK " + _pendingPath); Next(); }
                    else if (Now - _stageStart > 5.0)
                    {
                        // Game 뷰 탭이 가려져 있으면 파일이 안 생긴다 → 메인 카메라를 직접 찍는다 (오버레이 UI 는 빠짐)
                        try
                        {
                            Camera main = CaptureRenderer.MainCamera();
                            if (main == null) throw new InvalidOperationException("메인 카메라 없음");
                            CaptureRenderer.RenderCamera(main, W, H, _pendingPath);
                            Add("WARN Game 뷰 캡처가 안 돼 메인 카메라로 대신 찍음 (UI 없음)"); Add("OK " + _pendingPath);
                        }
                        catch (Exception e) { Add($"ERR {s.name}: {e.Message}"); }
                        Next();
                    }
                    return;
            }
        }

        private static void Next() { _idx++; _stage = 0; }

        private static void Shoot(CaptureShot s, bool playing)
        {
            string name = string.IsNullOrEmpty(s.name) ? "shot" + _idx : string.Concat(s.name.Split(Path.GetInvalidFileNameChars()));
            if (_timedOut) name += "_timeout";
            string path = Path.GetFullPath(Path.Combine(OutDir, name + ".png")).Replace('\\', '/');
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                switch (Mode(s))
                {
                    case "game":
                        if (playing && !Application.isBatchMode)
                        {
                            // 실제 Game 뷰 (UI 포함, 크기는 Game 뷰 해상도)
                            _pendingPath = path; _pendingStamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                            ScreenCapture.CaptureScreenshot(path);
                            _stageStart = Now; _stage = 3;
                            return;
                        }
                        Camera main = CaptureRenderer.MainCamera();
                        if (main == null) throw new InvalidOperationException("메인 카메라 없음");
                        CaptureRenderer.RenderCamera(main, W, H, path);
                        break;
                    case "scene":
                        SceneView sv = SceneView.lastActiveSceneView;
                        if (CaptureRenderer.GetPose(s, out Vector3 pos, out Quaternion rot, out _) == CaptureRenderer.Pose.Ok)
                            sv.camera.transform.SetPositionAndRotation(pos, rot);
                        CaptureRenderer.RenderCamera(sv.camera, W, H, path);
                        break;
                    case "free":
                        CaptureRenderer.RenderFree(s, W, H, path);
                        break;
                    default:
                        throw new InvalidOperationException($"모르는 mode '{s.mode}' (free | game | scene)");
                }
                Add("OK " + path);
            }
            catch (Exception e) { Add($"ERR {name}: {e.Message}"); }
            Next();
        }

        // Scene 뷰를 지정 시점으로 옮긴다 (시점이 없으면 지금 보이는 그대로). 오류면 메시지를 돌려준다.
        private static string AlignSceneView(CaptureShot s)
        {
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv == null) return "열려 있는 Scene 뷰가 없음 (배치모드에서는 scene 모드를 쓸 수 없음)";
            CaptureRenderer.Pose pose = CaptureRenderer.GetPose(s, out Vector3 pos, out Quaternion rot, out string err);
            if (pose == CaptureRenderer.Pose.Error) return err;
            if (pose == CaptureRenderer.Pose.Ok)
            {
                sv.LookAt(pos, rot, 5f, false, true);
                sv.pivot = pos + rot * Vector3.forward * sv.cameraDistance;   // 카메라가 정확히 pos 에 오도록
            }
            return null;
        }

        // ───────────────────────── 끝 ─────────────────────────
        private static void EndShots()
        {
            _shooting = false;
            ShaderUtil.allowAsyncCompilation = _prevAsync;
            if (EditorApplication.isPlaying && SessionState.GetBool(KOwnPlay, false)) ExitPlay();
            else Finish();
        }

        private static void Abort()
        {
            if (_shooting) { _shooting = false; ShaderUtil.allowAsyncCompilation = _prevAsync; }
            if (EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(KOwnPlay, false)) ExitPlay();
            else Finish();
        }

        private static void ExitPlay()
        {
            Phase = "exitPlay";
            SessionState.SetFloat(KDeadline, (float)(Now + 30.0));
            EditorApplication.ExitPlaymode();
        }

        private static void Add(string line) => SessionState.SetString(KResults, SessionState.GetString(KResults, "") + line + "\n");

        private static void Finish()
        {
            Phase = "";
            // 원래 열려 있던 씬으로 복귀 (플레이 중이거나 저장 안 된 변경이 있으면 건드리지 않음)
            string orig = SessionState.GetString(KOrig, "");
            if (orig != "" && !EditorApplication.isPlayingOrWillChangePlaymode && !AnyDirty())
            {
                string[] paths = orig.Split('|').Where(File.Exists).ToArray();
                for (int i = 0; i < paths.Length; i++) EditorSceneManager.OpenScene(paths[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
            }
            SessionState.SetString(KOrig, "");

            string results = SessionState.GetString(KResults, "");
            string[] lines = results.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int ok = lines.Count(l => l.StartsWith("OK ")), bad = lines.Count(l => l.StartsWith("ERR "));
            try { Directory.CreateDirectory(DefaultOutDir); File.WriteAllText(ResultFile, results + $"DONE ok={ok} err={bad}\n"); }
            catch (IOException e) { Debug.LogWarning("[Capture] 결과 파일을 못 씀: " + e.Message); }
            foreach (string l in lines)
            {
                if (l.StartsWith("OK ")) Debug.Log("[Capture] 저장: " + l.Substring(3));
                else if (l.StartsWith("ERR ")) Debug.LogError("[Capture] " + l.Substring(4));
                else Debug.LogWarning("[Capture] " + l);
            }
            Debug.Log($"[Capture] 끝: {ok}장 저장, 오류 {bad}건. 결과 목록: {Path.GetFullPath(ResultFile)}");
            if (Application.isBatchMode) EditorApplication.Exit(bad > 0 || ok == 0 ? 1 : 0);
        }
    }
}
