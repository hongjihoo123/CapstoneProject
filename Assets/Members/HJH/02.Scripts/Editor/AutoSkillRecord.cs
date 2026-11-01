using System.IO;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.EditorTools
{
    // 사람 손 안 대고 스킬 시연 영상 찍기 (보여주기용)
    //  Unity 를 명령줄로 띄울 때: Unity.exe -projectPath <프로젝트> -executeMethod Assets.Members.HJH._02.Scripts.EditorTools.AutoSkillRecord.Run
    //  → 정글 테스트 씬 열기 → 플레이 → SkillShowcaseDirector 로 14개 스킬 녹화 → 플레이 끄기 → Unity 닫기
    //  결과: Recordings/SkillShowcase_Auto.mp4, 결과 문구는 Library/FxBridgeOut/showcase_done.txt
    // 플레이에 들어갈 때 코드가 다시 불려도(도메인 리로드) 이어가도록 단계를 SessionState 에 저장
    [InitializeOnLoad]
    public static class AutoSkillRecord
    {
        private const string StepKey = "HJH_AutoRecord_Step";
        private const string TimeKey = "HJH_AutoRecord_Time";
        private const string ScenePath = "Assets/Members/HJH/01.Scene/Shader__Skill.unity";
        private const string VideoPath = "Recordings/SkillShowcase_Auto.mp4";
        private const string DoneFile = "Temp/FxBridge/showcase_done.txt";
        private const string ResultFile = "Library/FxBridgeOut/showcase_done.txt";

        static AutoSkillRecord()
        {
            if (SessionState.GetInt(StepKey, 0) > 0)
                EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (File.Exists(DoneFile))
                File.Delete(DoneFile);
            if (File.Exists(ResultFile))
                File.Delete(ResultFile);
            EditorSceneManager.OpenScene(ScenePath);
            SetStep(1);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void SetStep(int step)
        {
            SessionState.SetInt(StepKey, step);
            SessionState.SetFloat(TimeKey, (float)EditorApplication.timeSinceStartup);
        }

        private static float Elapsed => (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(TimeKey, 0f);

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            switch (SessionState.GetInt(StepKey, 0))
            {
                case 1:   // 열린 뒤 잠깐 기다렸다가 플레이
                    if (Elapsed < 3f)
                        return;
                    SetStep(2);
                    EditorApplication.EnterPlaymode();
                    break;

                case 2:   // 플레이가 자리 잡으면 시연 시작
                    if (!EditorApplication.isPlaying || Elapsed < 6f)
                        return;
                    SetStep(3);
                    SkillShowcaseDirector.Run(VideoPath);
                    break;

                case 3:   // 녹화 끝나면 플레이 끄기 (너무 오래 걸리면 포기)
                    if (!File.Exists(DoneFile) && Elapsed < 600f)
                        return;
                    Directory.CreateDirectory(Path.GetDirectoryName(ResultFile));
                    File.WriteAllText(ResultFile, File.Exists(DoneFile) ? File.ReadAllText(DoneFile) : "showcase: 시간 초과");
                    SetStep(4);
                    EditorApplication.ExitPlaymode();
                    break;

                case 4:   // 플레이가 꺼지면 Unity 닫기
                    if (EditorApplication.isPlaying)
                        return;
                    SessionState.EraseInt(StepKey);
                    EditorApplication.update -= Tick;
                    Debug.Log("[AutoSkillRecord] 끝 → Unity 종료");
                    EditorApplication.delayCall += () => EditorApplication.Exit(0);   // update 안에서 바로 닫으면 종료 중 튕김
                    break;
            }
        }
    }
}
