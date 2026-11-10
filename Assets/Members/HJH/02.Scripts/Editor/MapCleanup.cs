using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HJH.EditorTools
{
    // 새 돌 팩으로 바꾼 뒤 안 쓰는 Tripo 바위 파일(Rock_A / Rock_B 모델·텍스처·머티리얼·반투명 복사본)을 프로젝트 밖 백업 폴더로 옮긴다.
    // 삭제가 아니라 이동이라 필요하면 되돌릴 수 있다 (_RemovedBackup/날짜/ 아래에 같은 경로로 보관).
    // 안전장치: Rock_A/Rock_B 프리팹이 옛 파일을 아직 참조하거나, 다른 에셋이 그 파일을 참조하면 그 파일은 옮기지 않는다.
    public static class MapCleanup
    {
        private const string P = "Assets/Members/HJH/05.Data/MapProps/";
        private const string Fade = "Assets/Members/HJH/05.Data/Resources/HJH/RockFade/";

        [MenuItem("HJH/맵/정리/안 쓰는 Tripo 바위 파일 백업으로 이동")]
        private static void MoveUnusedTripoRocks()
        {
            if (EditorApplication.isPlaying) return;
            // 1) Rock_A / Rock_B 프리팹이 옛 파일을 아직 쓰면 중단
            foreach (var pf in new[] { P + "Prefabs/Rock_A.prefab", P + "Prefabs/Rock_B.prefab" })
            {
                var deps = AssetDatabase.GetDependencies(pf, true);
                var old = deps.FirstOrDefault(d => d.Contains("/Models/Rock_") || d.Contains("/Textures/Rock_") || d.Contains("/Materials/Rock_"));
                if (old != null) { Debug.LogWarning($"[Cleanup] {Path.GetFileName(pf)} 가 아직 옛 파일({old})을 참조합니다. '돌 → 1 새 돌 반입'을 다시 실행한 뒤 이 메뉴를 쓰세요."); return; }
            }

            var candidates = new List<string>();
            foreach (var n in new[] { "Rock_A", "Rock_B" })
            {
                candidates.Add($"{P}Models/{n}.fbx"); candidates.Add($"{P}Models/{n}_LOD1.fbx"); candidates.Add($"{P}Models/{n}_LOD2.fbx");
                candidates.Add($"{P}Textures/{n}_BaseColor.jpg"); candidates.Add($"{P}Materials/{n}_Mat.mat"); candidates.Add($"{Fade}{n}_Mat_Fade.mat");
            }
            candidates = candidates.Where(File.Exists).ToList();

            // 2) 다른 에셋이 참조하는지 검사
            var guids = new Dictionary<string, string>();
            foreach (var c in candidates)
            {
                var meta = File.ReadAllText(c + ".meta");
                int i = meta.IndexOf("guid: ", System.StringComparison.Ordinal);
                if (i >= 0) guids[c] = meta.Substring(i + 6, 32);
            }
            var exts = new[] { "*.unity", "*.prefab", "*.mat", "*.asset", "*.controller", "*.anim", "*.terrainlayer" };
            var referenced = new HashSet<string>();
            foreach (var ext in exts)
                foreach (var f in Directory.EnumerateFiles("Assets", ext, SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(f);
                    foreach (var kv in guids)
                        if (!referenced.Contains(kv.Key) && text.Contains(kv.Value) && Path.GetFullPath(f) != Path.GetFullPath(kv.Key)) referenced.Add(kv.Key);
                }

            string backup = Path.Combine("_RemovedBackup", System.DateTime.Now.ToString("yyyy-MM-dd"));
            int moved = 0; var skipped = new List<string>();
            foreach (var c in candidates)
            {
                if (referenced.Contains(c)) { skipped.Add(c); continue; }
                string dest = Path.Combine(backup, c);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Move(c, dest); if (File.Exists(c + ".meta")) File.Move(c + ".meta", dest + ".meta");
                moved++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Cleanup] Tripo 바위 파일 {moved}개를 {backup} 로 옮겼습니다 (되돌리려면 같은 경로로 복사). 다른 에셋이 참조해서 남긴 것 {skipped.Count}개{(skipped.Count > 0 ? ": " + string.Join(", ", skipped.Select(Path.GetFileName)) : "")}.");
        }
    }
}
