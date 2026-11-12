#if UNITY_EDITOR || PERF_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Char.TopDown;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.Char.Debugging
{
    // 스킬 하나씩 비용 재기. exe 인자 "-perfsweep <로그파일>" (+ -perfcap -1 로 상한 풀고 재야 차이가 보임)
    // 옵션: -perfsweepmap <씬> -perfsweepcasts <횟수> -perfsweepgap <초> -perfsweepfilter <이름 일부>
    // 스킬마다: Q 칸에 끼움 → 매 시전 전 다시 끼워 쿨타임 없앰 → 같은 자리·같은 조준점에 N번 시전. 적은 꺼 둠(조건 같게).
    // 창(window) 동안: 프레임 시간(평균·p95·최대), GPU·CPU 메인 스레드 시간(FrameTimingManager), 살아 있는 파티클·조명 최대치.
    public sealed class PerfSkillSweep : MonoBehaviour
    {
        private const string EditorFlag = "Captures/_perfsweep_on.txt";

        private string _out;
        private string _map = "Map_Shop";
        private int _casts = 6;
        private float _gap = 1.2f;
        private string _filter;

        // 창 단위 누적
        private readonly List<float> _frameMs = new();
        private readonly List<float> _gpuMs = new();
        private readonly List<float> _cpuMs = new();
        private int _peakParticles, _peakSystems, _peakLights, _peakFx;
        private float _nextScan;
        private bool _recording;
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private System.Diagnostics.Process _proc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
#if UNITY_EDITOR
            if (File.Exists(EditorFlag))
            {
                args = File.ReadAllText(EditorFlag).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                File.Delete(EditorFlag);
            }
#endif
            int i = Array.IndexOf(args, "-perfsweep");
            if (i < 0)
                return;

            var go = new GameObject("[PerfSkillSweep]");
            DontDestroyOnLoad(go);
            var sweep = go.AddComponent<PerfSkillSweep>();
            sweep._out = i + 1 < args.Length ? args[i + 1] : "perfsweep.txt";
            try { File.Delete(sweep._out); } catch { }

            string Arg(string key) { int k = Array.IndexOf(args, key); return k >= 0 && k + 1 < args.Length ? args[k + 1] : null; }
            if (Arg("-perfsweepmap") is { } m) sweep._map = m;
            if (Arg("-perfsweepcasts") is { } c) sweep._casts = int.Parse(c);
            if (Arg("-perfsweepgap") is { } g) sweep._gap = float.Parse(g, System.Globalization.CultureInfo.InvariantCulture);
            sweep._filter = Arg("-perfsweepfilter");
            if (Arg("-perfcap") is { } cap) Application.targetFrameRate = int.Parse(cap);
            Application.runInBackground = true;
        }

        private IEnumerator Start()
        {
            _proc = System.Diagnostics.Process.GetCurrentProcess();
            SkillData[] skills = LoadSkills();
            Line($"# sweep {DateTime.Now:HH:mm:ss} targetFps={Application.targetFrameRate} screen={Screen.width}x{Screen.height} map={_map} casts={_casts} gap={_gap} skills={skills.Length} frameTiming={FrameTimingManager.IsFeatureEnabled()}");
            Line("skill,frames,fps,avgMs,p95Ms,maxMs,gpuAvgMs,gpuP95Ms,cpuMainAvgMs,cpuPctOneCore,peakParticles,peakSystems,peakLights,peakFx,t0,t1");

            if (SceneManager.GetActiveScene().name != _map)
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(_map);
                if (op != null) while (!op.isDone) yield return null;
            }
            yield return new WaitForSecondsRealtime(4f);

            PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
            if (player == null) { Line("# ERR no player"); yield break; }
            foreach (AbstractEnemy e in FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
                e.gameObject.SetActive(false);
            Vector3 home = player.transform.position;
            Vector3 aim = home + new Vector3(4f, 0f, 3f);
            TopDownAimController.ScriptedAimPoint = aim;
            SkillData original = player.SkillFsm.GetSkill(SkillSlotId.Basic1);

            yield return new WaitForSecondsRealtime(2f);
            yield return Window("_baseline", null, player, home, aim);

            var input = new PressInput();
            foreach (SkillData skill in skills)
            {
                if (_filter != null && skill.name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                yield return Window(skill.name, skill, player, home, aim, input);
                // 남은 이펙트가 다음 스킬 창에 섞이지 않게 쉼
                yield return new WaitForSecondsRealtime(3f);
            }

            yield return Window("_baseline_end", null, player, home, aim);
            player.SkillFsm.Equip(SkillSlotId.Basic1, original);
            TopDownAimController.ScriptedAimPoint = null;
            Line($"# done {DateTime.Now:HH:mm:ss}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator Window(string label, SkillData skill, PlayerAgent player, Vector3 home, Vector3 aim, PressInput input = null)
        {
            BeginWindow();
            string t0 = DateTime.Now.ToString("HH:mm:ss.fff");
            for (int k = 0; k < _casts; k++)
            {
                if (player == null) break;
                player.Heal(player.Health.MaxHealth);
                if (skill != null)
                {
                    Teleport(player, home);
                    TopDownAimController.ScriptedAimPoint = aim;
                    player.SkillFsm.ForceIdle();
                    player.SkillFsm.Equip(SkillSlotId.Basic1, skill);   // 새로 끼우면 쿨타임 없음
                    try { player.SkillFsm.ResolveInput(input); }
                    catch (Exception e) { Line($"# {label} cast err {e.GetType().Name} {e.Message}"); }
                }
                yield return new WaitForSecondsRealtime(_gap);
            }
            string t1 = DateTime.Now.ToString("HH:mm:ss.fff");
            EndWindow(label, t0, t1);
        }

        private static void Teleport(PlayerAgent player, Vector3 position)
        {
            if (player.TryGetComponent(out CharacterController cc))
            {
                cc.enabled = false;
                player.transform.position = position;
                cc.enabled = true;
            }
            else
                player.transform.position = position;
        }

        private double _cpu0, _wall0;

        private void BeginWindow()
        {
            _frameMs.Clear(); _gpuMs.Clear(); _cpuMs.Clear();
            _peakParticles = _peakSystems = _peakLights = _peakFx = 0;
            _proc.Refresh();
            _cpu0 = _proc.TotalProcessorTime.TotalSeconds;
            _wall0 = Time.realtimeSinceStartupAsDouble;
            _recording = true;
        }

        private void EndWindow(string label, string t0, string t1)
        {
            _recording = false;
            _proc.Refresh();
            double wall = Time.realtimeSinceStartupAsDouble - _wall0;
            double cpuPct = (_proc.TotalProcessorTime.TotalSeconds - _cpu0) / Math.Max(1e-6, wall) * 100.0;
            Line(string.Join(",", label, _frameMs.Count, (_frameMs.Count / wall).ToString("F0"),
                Avg(_frameMs).ToString("F2"), P95(_frameMs).ToString("F2"), (_frameMs.Count > 0 ? _frameMs.Max() : 0).ToString("F1"),
                Avg(_gpuMs).ToString("F2"), P95(_gpuMs).ToString("F2"), Avg(_cpuMs).ToString("F2"), cpuPct.ToString("F0"),
                _peakParticles, _peakSystems, _peakLights, _peakFx, t0, t1));
        }

        private void Update()
        {
            if (!_recording) return;
            _frameMs.Add(Time.unscaledDeltaTime * 1000f);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
            {
                if (_timing[0].gpuFrameTime > 0) _gpuMs.Add((float)_timing[0].gpuFrameTime);
                if (_timing[0].cpuMainThreadFrameTime > 0) _cpuMs.Add((float)_timing[0].cpuMainThreadFrameTime);
            }

            if (Time.realtimeSinceStartup < _nextScan) return;
            _nextScan = Time.realtimeSinceStartup + 0.2f;
            int particles = 0, systems = 0;
            foreach (ParticleSystem ps in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                int n = ps.particleCount;
                if (n > 0) { particles += n; systems++; }
            }
            int lights = 0;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l.enabled && l.type != LightType.Directional) lights++;
            int fx = 0;
            GameObject root = GameObject.Find("[Fx]");
            if (root != null)
                foreach (Transform child in root.transform) if (child.gameObject.activeSelf) fx++;
            _peakParticles = Mathf.Max(_peakParticles, particles);
            _peakSystems = Mathf.Max(_peakSystems, systems);
            _peakLights = Mathf.Max(_peakLights, lights);
            _peakFx = Mathf.Max(_peakFx, fx);
        }

        private static float Avg(List<float> v) => v.Count > 0 ? v.Average() : 0f;

        private static float P95(List<float> v)
        {
            if (v.Count == 0) return 0f;
            var sorted = v.OrderBy(x => x).ToList();
            return sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Count * 0.95f) - 1, 0, sorted.Count - 1)];
        }

        private static SkillData[] LoadSkills()
        {
            var list = Resources.Load<PerfSkillList>("HJH/_PerfSkillList");
            if (list != null && list.skills != null)
                return list.skills.Where(s => s != null).ToArray();
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/Members/HJH/05.Data/Skill" })
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null).OrderBy(s => s.name).ToArray();
#else
            return Array.Empty<SkillData>();
#endif
        }

        private sealed class PressInput : ISkillInputSource
        {
            public bool WasSkillPressed(SkillSlotId slot) => slot == SkillSlotId.Basic1;
            public bool IsSkillHeld(SkillSlotId slot) => false;
            public bool WasSkillReleased(SkillSlotId slot) => slot == SkillSlotId.Basic1;
            public bool WasCancelPressed() => false;
            public bool WasJumpPressed() => false;
        }

        private void Line(string line)
        {
            try { File.AppendAllText(_out, line + "\n"); } catch { }
        }
    }
}
#endif
