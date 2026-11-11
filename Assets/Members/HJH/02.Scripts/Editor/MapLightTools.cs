using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HJH.EditorTools
{
    // 조명 도구 둘:
    //  1) 발광 꽃·버섯 무리마다 점광원을 달아서 주변 바위·땅이 그 색 빛을 받게 함 (씬에 이미 놓인 것 기준, 다시 돌리면 새로 만듦)
    //  2) 플레이어 프리팹(PlayerRig)에 위에서 내려다보는 Spot 라이트를 달아 줌 (프리팹이라 모든 맵에 적용)
    public static class MapLightTools
    {
        private const string RigPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";
        private const string GlowGroup = "GlowLights";
        private const string SpotName = "PlayerSpot";
        private const float Cell = 3.2f;
        private const int MaxLights = 60;

        // ───────────────────────── 1. 발광 오브젝트에 빛 ─────────────────────────
        [MenuItem("HJH/맵/조명/발광 꽃·버섯에 빛 달기 (주변 돌·땅이 빛 받게)")]
        private static void AddGlowLights()
        {
            if (EditorApplication.isPlaying) return;

            var old = GameObject.Find(GlowGroup);
            if (old != null) Undo.DestroyObjectImmediate(old);

            // 칸(Cell)별로 묶어서 무리마다 빛 하나
            var cells = new Dictionary<Vector2Int, (Vector3 sum, int n, Color color)>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Transform root = RootOfGlow(r.transform);
                if (root == null) continue;
                var key = new Vector2Int(Mathf.FloorToInt(r.transform.position.x / Cell), Mathf.FloorToInt(r.transform.position.z / Cell));
                Color c = GlowColor(r, root.name);
                cells.TryGetValue(key, out var cur);
                cells[key] = (cur.sum + r.transform.position, cur.n + 1, cur.n == 0 ? c : Color.Lerp(cur.color, c, 0.5f));
            }
            if (cells.Count == 0) { Debug.LogWarning("[Glow] 씬에서 GlowFlowers / Mushroom_ 를 찾지 못했습니다."); return; }

            var list = new List<KeyValuePair<Vector2Int, (Vector3 sum, int n, Color color)>>(cells);
            list.Sort((a, b) => b.Value.n.CompareTo(a.Value.n));   // 큰 무리부터, 너무 많으면 자름

            var group = new GameObject(GlowGroup);
            Undo.RegisterCreatedObjectUndo(group, "Glow lights");
            int made = 0;
            foreach (var kv in list)
            {
                if (made >= MaxLights) break;
                var (sum, n, color) = kv.Value;
                var go = new GameObject("GlowLight_" + made);
                go.transform.SetParent(group.transform, false);
                go.transform.position = sum / n + Vector3.up * 0.6f;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = color;
                l.range = 4.5f;
                l.intensity = 1.6f + 0.5f * Mathf.Min(n, 6);
                l.shadows = LightShadows.None;
                l.lightmapBakeType = LightmapBakeType.Mixed;
                made++;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Glow] 발광 꽃·버섯 {cells.Count}무리 중 큰 {made}곳에 점광원을 달았습니다 (그룹 {GlowGroup}). 베이크는 마지막에 한 번만 하세요.");
        }

        private static Transform RootOfGlow(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.StartsWith("GlowFlowers") || t.name.StartsWith("Mushroom_")) return t;
            return null;
        }

        private static Color GlowColor(MeshRenderer r, string rootName)
        {
            if (rootName.StartsWith("Mushroom_"))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !m.HasProperty("_EmissionColor")) continue;
                    Color e = m.GetColor("_EmissionColor");
                    float mx = Mathf.Max(e.r, e.g, e.b);
                    if (mx > 0.1f) return new Color(e.r / mx, e.g / mx, e.b / mx);
                }
                return new Color(0.3f, 1f, 0.9f);
            }
            return new Color(0.3f, 0.62f, 1f);   // 파란 꽃
        }

        // ───────────────────────── 2. 플레이어 Spot 라이트 ─────────────────────────
        [MenuItem("HJH/플레이어/Spot 라이트 달기 (PlayerRig 프리팹)")]
        private static void AddPlayerSpotMenu()
        {
            if (EditorApplication.isPlaying) return;
            var root = PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                if (!AddPlayerSpot(root.transform)) { Debug.LogWarning("[Spot] PlayerRig 안에서 Player 를 못 찾았습니다."); return; }
                PrefabUtility.SaveAsPrefabAsset(root, RigPath);
                Debug.Log("[Spot] PlayerRig 프리팹에 Spot 라이트를 달았습니다. 플레이어 → PlayerSpot 의 Light 값(밝기·각도·그림자)을 맘대로 조절하세요. 모든 맵의 플레이어에 적용됩니다.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // PlayerRigBuilder 가 프리팹을 새로 만들 때도 부름 (다시 만들어도 Spot 이 유지되게)
        internal static bool AddPlayerSpot(Transform rigRoot)
        {
            Transform player = rigRoot.name == "Player" ? rigRoot : rigRoot.Find("Player");
            if (player == null) return false;

            Transform spot = player.Find(SpotName);
            if (spot == null)
            {
                var go = new GameObject(SpotName);
                go.transform.SetParent(player, false);
                spot = go.transform;
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = new Color(1f, 0.88f, 0.7f);
                l.spotAngle = 78f;
                l.innerSpotAngle = 34f;
                l.range = 15f;
                l.intensity = 45f;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.8f;
                l.lightmapBakeType = LightmapBakeType.Realtime;
            }
            // 위에서 수직으로 내려다봄 (플레이어가 돌아도 빛 방향은 그대로 아래)
            spot.localPosition = new Vector3(0f, 7f, 0f);
            spot.rotation = Quaternion.Euler(90f, 0f, 0f);
            return true;
        }
    }
}
