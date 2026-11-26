using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJH.CaptureTool
{
    [InitializeOnLoad]
    internal static class CaptureRunner
    {
        public const string DefaultOutDir = "Captures";
        public const string RequestFile = DefaultOutDir + "/_request.txt";
        public const string ResultFile = DefaultOutDir + "/_result.txt";
        public const string LastConfigPref = "HJH.Capture.LastConfig";

        private const string KPhase = "HJH.Capture.Phase", KConfig = "HJH.Capture.Config", KResults = "HJH.Capture.Results",
            KOrig = "HJH.Capture.OrigScenes", KDeadline = "HJH.Capture.Deadline", KOwnPlay = "HJH.Capture.OwnPlay", KStart = "HJH.Capture.Start";

        private static CaptureConfig _cfg;
        private static bool _shooting, _timedOut, _prevAsync;
        private static int _idx, _stage, _frames;
        private static double _stageStart, _nextPoll;
        private static string _pendingPath; private static DateTime _pendingStamp;

        private static string Phase { get => SessionState.GetString(KPhase, ""); set => SessionState.SetString(KPhase, value); }
        private static double Now => EditorApplication.timeSinceStartup;

        static CaptureRunner() { EditorApplication.update += Tick; }

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
            full = Path.GetFullPath(configPath).Replace('\\', '/');
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
            try { config = File.ReadAllText(RequestFile).Trim(); File.Delete(RequestFile); }
            catch (IOException) { return; }
            if (config == "refresh") { WriteResult("OK refresh\n"); AssetDatabase.Refresh(); return; }
            if (config.StartsWith("menu:")) { RunMenu(config.Substring(5).Trim()); return; }
            Start(config, false);
        }

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

        private static void BeginShots()
        {
            _idx = 0; _stage = 0; _shooting = true;
            _prevAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            if (EditorApplication.isPlaying) Application.runInBackground = true;
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
                case 0:
                    if (playing && Time.time < s.delay) return;
                    if (playing && !string.IsNullOrEmpty(s.forceState) && !CaptureRenderer.ForceState(s.target, s.forceState))
                        Add($"WARN {s.name}: forceState '{s.forceState}' 를 가진 Animator 를 못 찾음");
                    _stageStart = Now; _timedOut = false; _stage = 1;
                    return;
                case 1:
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
                case 2:
                    if (Mode(s) == "scene" && SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
                    if (--_frames > 0) return;
                    Shoot(s, playing);
                    return;
                case 3:
                    if (File.Exists(_pendingPath) && File.GetLastWriteTimeUtc(_pendingPath) > _pendingStamp && new FileInfo(_pendingPath).Length > 0)
                    { Add("OK " + _pendingPath); Next(); }
                    else if (Now - _stageStart > 5.0)
                    {
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

        private static string AlignSceneView(CaptureShot s)
        {
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv == null) return "열려 있는 Scene 뷰가 없음 (배치모드에서는 scene 모드를 쓸 수 없음)";
            CaptureRenderer.Pose pose = CaptureRenderer.GetPose(s, out Vector3 pos, out Quaternion rot, out string err);
            if (pose == CaptureRenderer.Pose.Error) return err;
            if (pose == CaptureRenderer.Pose.Ok)
            {
                sv.LookAt(pos, rot, 5f, false, true);
                sv.pivot = pos + rot * Vector3.forward * sv.cameraDistance;
            }
            return null;
        }

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
