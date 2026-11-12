#if UNITY_EDITOR || PERF_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01.Scripts.Player;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.Char.Debugging
{
    // 성능·누수 측정용 (팬 소음 조사). 평소엔 아무것도 안 함.
    // 켜는 법: exe 실행 인자 "-perfprobe <로그파일>" (PERF_PROBE 정의로 만든 측정용 빌드에만 들어감),
    //          에디터는 Captures/_perfprobe_on.txt 에 같은 인자를 써 두고 플레이.
    // 옵션: -perftour <바퀴 수> -perfstay <맵당 초> -perfmaps A,B,C -perfskills 0|1 -perfcap <fps> -perfvsync 0|1
    // 매 초: 프레임 시간, 프로세스 CPU, GC, 메모리. 맵마다 떠나기 직전: 오브젝트·에셋 수, DDOL 루트, static 이벤트·컬렉션 수.
    public sealed class PerfProbe : MonoBehaviour
    {
        private const string EditorFlag = "Captures/_perfprobe_on.txt";

        private string _out;
        private int _rounds = 3;
        private float _stay = 25f;
        private bool _skills = true;
        private string[] _maps = { "Map_Bridge_Normal", "Map_L_Normal", "Map_Shop", "_HJH_Map" };

        private System.Diagnostics.Process _proc;
        private ProfilerRecorder _gcAlloc, _gcUsed, _gfxUsed, _sysUsed, _totalUsed;

        // 초 단위 누적
        private int _frames;
        private float _sumDt, _maxDt;
        private long _allocBytes;
        private double _cpuLast, _wallLast;
        private int _gcLast;
        private float _secStart;
        private int _visit;

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
            int i = Array.IndexOf(args, "-perfprobe");
            if (i < 0)
                return;

            var go = new GameObject("[PerfProbe]");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<PerfProbe>();
            probe._out = i + 1 < args.Length ? args[i + 1] : "perfprobe.txt";
            try { File.Delete(probe._out); } catch { }
            probe.Parse(args);
        }

        private void Parse(string[] args)
        {
            string Arg(string key) { int k = Array.IndexOf(args, key); return k >= 0 && k + 1 < args.Length ? args[k + 1] : null; }
            if (Arg("-perftour") is { } r) _rounds = int.Parse(r);
            if (Arg("-perfstay") is { } s) _stay = float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
            if (Arg("-perfskills") is { } k) _skills = k != "0";
            if (Arg("-perfmaps") is { } m) _maps = m.Split(',');
            if (Arg("-perfcap") is { } c) Application.targetFrameRate = int.Parse(c);
            if (Arg("-perfvsync") is { } v) QualitySettings.vSyncCount = int.Parse(v);
            Application.runInBackground = true;
        }

        private void Start()
        {
            _proc = System.Diagnostics.Process.GetCurrentProcess();
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _gfxUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
            _sysUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _totalUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");

            Line($"# start {DateTime.Now:HH:mm:ss} editor={Application.isEditor} vSync={QualitySettings.vSyncCount} targetFps={Application.targetFrameRate} " +
                 $"quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} screen={Screen.width}x{Screen.height} cores={SystemInfo.processorCount} " +
                 $"gpu={SystemInfo.graphicsDeviceName} skills={_skills} rounds={_rounds} stay={_stay} maps={string.Join(",", _maps)} gcAllocRecorder={_gcAlloc.Valid}");
            Line("t,visit,scene,fps,avgMs,maxMs,cpuPctOneCore,gcRuns,allocKB,gcUsedMB,totalUsedMB,gfxUsedMB,sysUsedMB,fxActive");
            ResetSecond();
            StartCoroutine(Tour());
            if (_skills)
                StartCoroutine(SkillLoop());
        }

        private void OnDestroy()
        {
            _gcAlloc.Dispose(); _gcUsed.Dispose(); _gfxUsed.Dispose(); _sysUsed.Dispose(); _totalUsed.Dispose();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _frames++;
            _sumDt += dt;
            if (dt > _maxDt) _maxDt = dt;
            if (_gcAlloc.Valid) _allocBytes += _gcAlloc.LastValue;

            if (Time.realtimeSinceStartup - _secStart >= 1f)
                FlushSecond();
        }

        private void ResetSecond()
        {
            _frames = 0; _sumDt = 0; _maxDt = 0; _allocBytes = 0;
            _proc.Refresh();
            _cpuLast = _proc.TotalProcessorTime.TotalSeconds;
            _wallLast = Time.realtimeSinceStartupAsDouble;
            _gcLast = GC.CollectionCount(0);
            _secStart = Time.realtimeSinceStartup;
        }

        private void FlushSecond()
        {
            _proc.Refresh();
            double cpu = _proc.TotalProcessorTime.TotalSeconds, wall = Time.realtimeSinceStartupAsDouble;
            double cpuPct = (cpu - _cpuLast) / Math.Max(1e-6, wall - _wallLast) * 100.0;
            int gcRuns = GC.CollectionCount(0) - _gcLast;
            float avg = _frames > 0 ? _sumDt / _frames : 0f;
            Transform fx = FindFx();
            int fxActive = 0;
            if (fx != null)
                foreach (Transform child in fx) if (child.gameObject.activeSelf) fxActive++;

            Line(string.Join(",",
                Time.realtimeSinceStartup.ToString("F0"), _visit, SceneManager.GetActiveScene().name,
                (_frames / (float)(wall - _wallLast)).ToString("F0"), (avg * 1000f).ToString("F2"), (_maxDt * 1000f).ToString("F1"),
                cpuPct.ToString("F0"), gcRuns, _gcAlloc.Valid ? (_allocBytes / 1024).ToString() : "na",
                Mb(_gcUsed), Mb(_totalUsed), Mb(_gfxUsed), Mb(_sysUsed), fxActive));
            ResetSecond();
        }

        private Transform _fx;
        private Transform FindFx()
        {
            if (_fx == null) { var go = GameObject.Find("[Fx]"); _fx = go != null ? go.transform : null; }
            return _fx;
        }

        private static string Mb(ProfilerRecorder r) => r.Valid ? (r.LastValue / (1024f * 1024f)).ToString("F1") : "na";

        private IEnumerator Tour()
        {
            yield return Stay();   // 처음 열린 씬
            for (int round = 0; round < _rounds; round++)
            {
                foreach (string map in _maps)
                {
                    Snapshot();
                    _visit++;
                    Line($"# load {map} (visit {_visit}, round {round + 1})");
                    AsyncOperation op = SceneManager.LoadSceneAsync(map);
                    if (op == null) { Line($"# ERR cannot load {map}"); continue; }
                    while (!op.isDone) yield return null;
                    _fx = null;
                    yield return Stay();
                }
            }
            Snapshot();
            Line($"# done {DateTime.Now:HH:mm:ss}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator Stay()
        {
            float end = Time.realtimeSinceStartup + _stay;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        // 스킬 계속 쓰기 (가짜 입력 한 프레임) + 안 죽게 회복
        private IEnumerator SkillLoop()
        {
            var input = new FakeInput();
            SkillSlotId[] order = { SkillSlotId.Basic1, SkillSlotId.Basic2, SkillSlotId.Weapon, SkillSlotId.Ultimate, SkillSlotId.Dash };
            int n = 0;
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
                if (player == null || player.SkillFsm == null || player.Health == null) continue;
                player.Heal(player.Health.MaxHealth);
                if (player.Health.IsDead) continue;
                input.Slot = order[n++ % order.Length];
                try { player.SkillFsm.ResolveInput(input); }
                catch (Exception e) { Line("# skill err " + e.GetType().Name + " " + e.Message); }
            }
        }

        private sealed class FakeInput : ISkillInputSource
        {
            public SkillSlotId Slot;
            public bool WasSkillPressed(SkillSlotId slot) => slot == Slot;
            public bool IsSkillHeld(SkillSlotId slot) => false;
            public bool WasSkillReleased(SkillSlotId slot) => slot == Slot;
            public bool WasCancelPressed() => false;
            public bool WasJumpPressed() => false;
        }

        // ── 누수 점검: 맵을 떠나기 직전에 센다
        private void Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append($"# snap visit={_visit} scene={SceneManager.GetActiveScene().name}");
            sb.Append($" GameObject={Resources.FindObjectsOfTypeAll<GameObject>().Length}");
            sb.Append($" MonoBehaviour={Resources.FindObjectsOfTypeAll<MonoBehaviour>().Length}");
            sb.Append($" Material={Resources.FindObjectsOfTypeAll<Material>().Length}");
            sb.Append($" Texture2D={Resources.FindObjectsOfTypeAll<Texture2D>().Length}");
            sb.Append($" RenderTexture={Resources.FindObjectsOfTypeAll<RenderTexture>().Length}");
            sb.Append($" Mesh={Resources.FindObjectsOfTypeAll<Mesh>().Length}");
            sb.Append($" VolumeProfile={Resources.FindObjectsOfTypeAll<VolumeProfile>().Length}");
            sb.Append($" Volume={Resources.FindObjectsOfTypeAll<Volume>().Length}");
            sb.Append($" ParticleSystem={Resources.FindObjectsOfTypeAll<ParticleSystem>().Length}");
            sb.Append($" AudioSource={Resources.FindObjectsOfTypeAll<AudioSource>().Length}");
            sb.Append($" ScriptableObject={Resources.FindObjectsOfTypeAll<ScriptableObject>().Length}");
            sb.Append($" monoHeapMB={Profiler.GetMonoHeapSizeLong() / 1048576f:F1} monoUsedMB={Profiler.GetMonoUsedSizeLong() / 1048576f:F1}");
            Line(sb.ToString());

            GameObject[] ddol = gameObject.scene.GetRootGameObjects();
            Line($"# ddol {ddol.Length}: " + string.Join(" | ", ddol.Select(g => g.name)));
            Line("# statics " + StaticCounts());
        }

        // 우리 코드의 static 이벤트(구독 수)와 static 컬렉션(개수). 맵 돌수록 커지면 해제 누락
        private static string StaticCounts()
        {
            var parts = new List<string>();
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name;
                if (!(an.StartsWith("Assembly-CSharp") || an is "PoolRuntime" or "EventChannelSystem" or "ModuleSystem" or "AnimatorSystem"))
                    continue;
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (Type type in types)
                {
                    string ns = type.Namespace ?? "";
                    if (type.ContainsGenericParameters || ns.StartsWith("Unity") || ns.StartsWith("DG") || ns.StartsWith("TMPro")
                        || ns.Contains(".Editor") || type == typeof(PerfProbe))
                        continue;
                    foreach (FieldInfo f in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsLiteral) continue;
                        bool isDelegate = typeof(Delegate).IsAssignableFrom(f.FieldType);
                        bool isCollection = typeof(System.Collections.ICollection).IsAssignableFrom(f.FieldType);
                        if (!isDelegate && !isCollection) continue;
                        if (f.FieldType.IsArray) continue;
                        object value;
                        try { value = f.GetValue(null); } catch { continue; }
                        int count = value switch
                        {
                            Delegate d => d.GetInvocationList().Length,
                            System.Collections.ICollection c => c.Count,
                            _ => 0
                        };
                        if (count > 0)
                            parts.Add($"{type.Name}.{f.Name}={count}");
                    }
                }
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join(" ", parts);
        }

        private void Line(string line)
        {
            try { File.AppendAllText(_out, line + "\n"); } catch { /* 경로 문제면 무시 */ }
        }
    }
}
#endif
