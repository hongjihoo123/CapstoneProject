using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HJH.CaptureTool
{
    public static class CaptureMenu
    {
        private const string ConfigDir = "Assets/Members/HJH/02.Scripts/Editor/Capture/Configs";

        [MenuItem("HJH/캡처/설정 파일 골라서 찍기...")]
        private static void Pick()
        {
            string path = EditorUtility.OpenFilePanel("캡처 설정(JSON)", ConfigDir, "json");
            if (!string.IsNullOrEmpty(path)) CaptureRunner.Start(path, true);
        }

        [MenuItem("HJH/캡처/마지막 설정으로 다시 찍기")]
        private static void Repeat()
        {
            string last = EditorPrefs.GetString(CaptureRunner.LastConfigPref, "");
            if (last == "") { Debug.LogWarning("[Capture] 아직 찍은 적이 없습니다. 먼저 설정 파일을 골라 주세요."); return; }
            CaptureRunner.Start(last, true);
        }

        [MenuItem("HJH/캡처/예시 - 다리 맵 플레이어 정면·측면 (편집 모드)")]
        private static void ExampleEdit() => CaptureRunner.Start(ConfigDir + "/bridge_player.json", true);

        [MenuItem("HJH/캡처/예시 - 다리 맵 플레이어 (플레이 3초 뒤 + Run 대기)")]
        private static void ExamplePlay() => CaptureRunner.Start(ConfigDir + "/bridge_player_play.json", true);

        [MenuItem("HJH/캡처/저장 폴더 열기")]
        private static void Reveal()
        {
            Directory.CreateDirectory(CaptureRunner.DefaultOutDir);
            EditorUtility.RevealInFinder(Path.GetFullPath(CaptureRunner.ResultFile));
        }

        public static void RunFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-captureConfig");
            if (i < 0 || i + 1 >= args.Length) { Debug.LogError("[Capture] -captureConfig <json 경로> 가 필요합니다."); EditorApplication.Exit(2); return; }
            CaptureRunner.Start(args[i + 1], false);
        }
    }
}
