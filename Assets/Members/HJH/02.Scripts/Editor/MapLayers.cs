using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HJH.EditorTools
{
    // 총알이 닿는 레이어(RobotWeapons.BulletLayers: Obstacle / Ground / Enemy)에 맞게 맵 오브젝트 레이어를 정리.
    //   지형(Terrain), 다리 바닥(Deck)                       -> Ground
    //   그 밖의 콜라이더 달린 오브젝트(돌·나무·통나무·버섯·암벽 등, Default 레이어인 것만) -> Obstacle
    //   보이지 않는 경계 벽(Boundary_Walls / Wall_*)           -> Default 그대로 (총알은 통과, 플레이어·대시는 계속 막음)
    // '돌/5 콜라이더 정리' 끝에서도 자동으로 부르고, 단독 메뉴도 있음. 되돌리기는 Ctrl+Z.
    public static class MapLayers
    {
        [MenuItem("HJH/맵/돌/7 레이어 정리 (지형=Ground, 돌·통나무·버섯·암벽=Obstacle)")]
        private static void Menu() => Assign();

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
                if (col.gameObject.layer != 0 && want == obstacle) continue;   // 이미 Water/Enemy/UI 같은 레이어를 따로 준 것은 건드리지 않음 (Default 만 바꿈)
                Undo.RecordObject(col.gameObject, "Layer");
                col.gameObject.layer = want;
                if (want == ground) groundN++; else obstacleN++;
            }

            Undo.CollapseUndoOperations(grp);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Layers] 바뀐 것: Ground {groundN}개, Obstacle {obstacleN}개. 경계 벽(Boundary_Walls)은 Default 그대로라 총알이 통과합니다. (Ctrl+Z 로 되돌릴 수 있음)");
        }

        // -1 = 건드리지 않음
        private static int LayerFor(Transform t, int ground, int obstacle)
        {
            // 건드리지 않을 것: 보이지 않는 벽, 플레이어, 포탈·스폰·상점·문, 물·안개 (위로 올라가며 한 번이라도 걸리면 제외)
            for (Transform p = t; p != null; p = p.parent)
            {
                string n = p.name;
                if (n == "Boundary_Walls" || n.StartsWith("Wall_") || n == "PlayerRig" || n == "Player") return -1;
                if (n.Contains("Portal") || n.Contains("Spawn") || n.Contains("Shop") || n.Contains("Door") || n.Contains("Water") || n.Contains("Fog")) return -1;
            }
            for (Transform p = t; p != null; p = p.parent)
                if (p.name == "Deck" || p.name == "Bridge") return ground;
            return obstacle;   // 나머지 콜라이더 달린 오브젝트(돌·나무·통나무·버섯·암벽 등) 전부
        }
    }
}
