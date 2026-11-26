using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    public static class MapLayers
    {
        [MenuItem("HJH/맵/돌/7 레이어 정리 (지형=Ground, 돌·통나무·버섯·암벽=Obstacle)")]
        private static void Menu() => Assign();

        [MenuItem("HJH/맵/돌/8 레이어 정리 (HJH 폴더의 모든 맵 씬)")]
        private static void MenuAll()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            const string sceneDir = "Assets/Members/HJH/01.Scene/";
            string original = EditorSceneManager.GetActiveScene().path;
            var paths = System.IO.Directory.GetFiles(sceneDir, "*.unity").Select(p => p.Replace('\\', '/'))
                .Where(p => !System.IO.Path.GetFileName(p).StartsWith("Shader") && !System.IO.Path.GetFileName(p).StartsWith("_Shader")).ToList();

            string backup = "_RemovedBackup/" + System.DateTime.Now.ToString("yyyy-MM-dd") + "/" + sceneDir;
            System.IO.Directory.CreateDirectory(backup);
            foreach (string p in paths) System.IO.File.Copy(p, backup + System.IO.Path.GetFileName(p), true);

            foreach (string p in paths)
            {
                var sc = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                Assign();
                if (sc.isDirty) EditorSceneManager.SaveScene(sc);
            }
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            Debug.Log($"[Layers] 모든 맵 씬 {paths.Count}개 정리 완료 (씬 백업: {backup}). 씬별 결과는 위 로그를 보세요.");
        }

        internal static void Assign()
        {
            if (EditorApplication.isPlaying) return;
            int ground = LayerMask.NameToLayer("Ground"), obstacle = LayerMask.NameToLayer("Obstacle");
            if (ground < 0 || obstacle < 0)
            {
                Debug.LogWarning("[Layers] Ground / Obstacle 레이어가 프로젝트에 없습니다 (Project Settings > Tags and Layers).");
                return;
            }

            Undo.IncrementCurrentGroup(); int grp = Undo.GetCurrentGroup();
            int groundN = 0, obstacleN = 0;

            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                if (terrain.gameObject.layer != ground) { Undo.RecordObject(terrain.gameObject, "Layer"); terrain.gameObject.layer = ground; groundN++; }

            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col is TerrainCollider || col.isTrigger) continue;
                int want = LayerFor(col.transform, ground, obstacle);
                if (want < 0 || col.gameObject.layer == want) continue;
                if (col.gameObject.layer != 0 && want == obstacle) continue;
                Undo.RecordObject(col.gameObject, "Layer");
                col.gameObject.layer = want;
                if (want == ground) groundN++; else obstacleN++;
            }

            Undo.CollapseUndoOperations(grp);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Layers] 바뀐 것: Ground {groundN}개, Obstacle {obstacleN}개. 경계 벽(Boundary_Walls)은 Default 그대로라 총알이 통과합니다. (Ctrl+Z 로 되돌릴 수 있음)");
        }

        private static int LayerFor(Transform t, int ground, int obstacle)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                string n = p.name;
                if (n == "Boundary_Walls" || n.StartsWith("Wall_") || n == "PlayerRig" || n == "Player") return -1;
                if (n.Contains("Portal") || n.Contains("Spawn") || n.Contains("Shop") || n.Contains("Door") || n.Contains("Water") || n.Contains("Fog")) return -1;
            }
            for (Transform p = t; p != null; p = p.parent)
                if (p.name == "Deck" || p.name == "Bridge") return ground;
            return obstacle;
        }
    }
}
