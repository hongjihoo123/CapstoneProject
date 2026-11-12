using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HJH.EditorTools
{
    // 성능 측정용 exe 빌드 (PerfProbe 포함, PERF_PROBE 정의). 결과: Temp/PerfBuild/Game.exe, 로그 Captures/_perfbuild.txt
    // 실행 예: Game.exe -perfprobe C:\...\probe.txt -perftour 3 -perfstay 25 -screen-width 1920 -screen-height 1080
    public static class PerfProbeBuild
    {
        [MenuItem("HJH/Perf/측정용 빌드")]
        public static void Build()
        {
            const string log = "Captures/_perfbuild.txt";
            Directory.CreateDirectory("Captures");
            if (EditorApplication.isPlaying) { File.WriteAllText(log, "ERR 플레이 중"); return; }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            File.WriteAllText(log, "building " + string.Join(", ", scenes) + "\n");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Temp/PerfBuild/Game.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { "PERF_PROBE" }
            };
            // 빌드 씬에 없는 스킬도 exe에서 재 보려고 스킬 목록을 Resources에 잠깐 만듦 + GPU 시간 재려고 Frame Timing Stats 켬. 끝나면 둘 다 되돌림
            const string listPath = "Assets/Members/HJH/05.Data/Resources/HJH/_PerfSkillList.asset";
            bool timingWas = PlayerSettings.enableFrameTimingStats;
            BuildReport report;
            try
            {
                var list = ScriptableObject.CreateInstance<Assets.Members.HJH._02.Scripts.Char.Debugging.PerfSkillList>();
                list.skills = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/Members/HJH/05.Data/Skill" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillData>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(s => s != null).OrderBy(s => s.name).ToArray();
                AssetDatabase.CreateAsset(list, listPath);
                PlayerSettings.enableFrameTimingStats = true;
                File.AppendAllText(log, $"skills {list.skills.Length}\n");
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                AssetDatabase.DeleteAsset(listPath);
                PlayerSettings.enableFrameTimingStats = timingWas;
                AssetDatabase.SaveAssets();
            }
            File.AppendAllText(log, $"{report.summary.result} {report.summary.totalTime} errors={report.summary.totalErrors} size={report.summary.totalSize / 1048576}MB\n");
            Debug.Log("[PerfProbeBuild] " + report.summary.result);
        }
    }
}
