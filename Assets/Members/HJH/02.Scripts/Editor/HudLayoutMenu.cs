using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HJH.EditorTools
{
    public static class HudLayoutMenu
    {
        private const string PlayerRigPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";

        [MenuItem("HJH/UI/진단/HUD 배치 덤프")]
        public static void Dump()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            var text = new StringBuilder();
            try
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true).Where(c => c.isRootCanvas))
                {
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    text.AppendLine($"CANVAS {Path(canvas.transform, root.transform)} mode={canvas.renderMode} ref={(scaler != null ? scaler.referenceResolution.ToString() : "-")} match={(scaler != null ? scaler.matchWidthOrHeight : -1)}");
                    Walk(canvas.transform, 1, 3, text);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Directory.CreateDirectory("Captures");
            File.WriteAllText("Captures/_hud_dump.txt", text.ToString());
            Debug.Log("[HudLayout] Captures/_hud_dump.txt");
        }

        [MenuItem("HJH/UI/HUD 배치 (11-23 스케치)")]
        public static void Arrange()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[HudLayout] 플레이 중에는 안 함.");
                return;
            }

            Directory.CreateDirectory("Assets/Members/HJH/Backups");
            File.Copy(PlayerRigPath, $"Assets/Members/HJH/Backups/PlayerRig_before_hudlayout_{System.DateTime.Now:MMdd_HHmm}.prefab.bak", true);

            var log = new StringBuilder();
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            try
            {
                var anchorLeftBottom = new Vector2(0f, 0f);
                var anchorLeftTop = new Vector2(0f, 1f);
                var anchorRightTop = new Vector2(1f, 1f);

                RectTransform hp = Find(root, "PlayerHpHud", log);
                Transform hud = hp != null ? hp.parent : null;
                RectTransform portrait = hud != null ? EnsurePortrait(hud, log) : null;
                Set(portrait, anchorLeftBottom, anchorLeftBottom, new Vector2(32f, 28f), log);
                Set(hp, anchorLeftBottom, anchorLeftBottom, new Vector2(160f, 82f), log);
                Set(Find(root, "GoldHud", log), anchorLeftBottom, anchorLeftBottom, new Vector2(160f, 28f), log);
                Set(Find(root, "ComboBuffBar", log), anchorLeftBottom, anchorLeftBottom, new Vector2(32f, 156f), log);

                Set(Find(root, "ComboList", log), anchorLeftTop, anchorLeftTop, new Vector2(32f, -28f), log);

                Set(Find(root, "MiniMap", log), anchorRightTop, new Vector2(0.5f, 0.5f), new Vector2(-135f, -135f), log);
                Set(Find(root, "MiniMapFrame", log), anchorRightTop, new Vector2(0.5f, 0.5f), new Vector2(-135f, -135f), log);

                var anchorRightBottom = new Vector2(1f, 0f);
                Set(Find(root, "StatsMiniHud", log), anchorRightBottom, anchorRightBottom, new Vector2(-48f, 84f), log);

                Set(Find(root, "KillQuestHud", log), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), log);

                RectTransform skillBar = Find(root, "SkillBar", log);
                RectTransform passive = skillBar != null ? skillBar.Find("Passive") as RectTransform : null;
                if (passive != null)
                {
                    passive.anchoredPosition = new Vector2(-310f, 44f);
                    log.AppendLine("Passive -> (-310, 44)");
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerRigPath);
                log.AppendLine("DONE");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            RevertSceneOverrides(log);
            Directory.CreateDirectory("Captures");
            File.WriteAllText("Captures/_hud_layout_log.txt", log.ToString());
            Debug.Log("[HudLayout]\n" + log);
        }

        private static readonly string[] Managed =
        {
            "PlayerPortrait", "PlayerHpHud", "GoldHud", "ComboBuffBar", "ComboList", "MiniMap", "MiniMapFrame", "StatsMiniHud", "KillQuestHud", "Passive"
        };

        private static readonly string[] PositionProps = { "m_AnchoredPosition", "m_AnchorMin", "m_AnchorMax", "m_Pivot" };

        [MenuItem("HJH/UI/HUD 배치 - 열린 씬 위치 덮어쓰기 되돌리기")]
        public static void RevertOpenSceneOverrides()
        {
            var log = new StringBuilder();
            RevertSceneOverrides(log);
            Directory.CreateDirectory("Captures");
            File.WriteAllText("Captures/_hud_layout_log.txt", log.ToString());
            Debug.Log("[HudLayout]\n" + log);
        }

        private static void RevertSceneOverrides(StringBuilder log)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;
                foreach (GameObject top in scene.GetRootGameObjects())
                {
                    foreach (RectTransform rect in top.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (!Managed.Contains(rect.name) || !PrefabUtility.IsPartOfPrefabInstance(rect))
                            continue;
                        var so = new SerializedObject(rect);
                        foreach (string prop in PositionProps)
                        {
                            SerializedProperty p = so.FindProperty(prop);
                            if (p == null || !p.prefabOverride)
                                continue;
                            PrefabUtility.RevertPropertyOverride(p, InteractionMode.UserAction);
                            log.AppendLine($"씬 {scene.name}: {rect.name}.{prop} 덮어쓰기 되돌림");
                        }
                    }
                }
            }
        }

        private static RectTransform Find(GameObject root, string name, StringBuilder log)
        {
            RectTransform found = root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);
            if (found == null)
                log.AppendLine("없음 " + name);
            return found;
        }

        private static void Set(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, StringBuilder log)
        {
            if (rect == null)
                return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            log.AppendLine($"{rect.name} -> anchor {anchor} pivot {pivot} pos {pos}");
        }

        private static RectTransform EnsurePortrait(Transform hud, StringBuilder log)
        {
            Transform existing = hud.Find("PlayerPortrait");
            if (existing != null)
                return (RectTransform)existing;

            var rect = new GameObject("PlayerPortrait", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(hud, false);
            rect.sizeDelta = new Vector2(112f, 112f);
            var back = rect.gameObject.AddComponent<Image>();
            back.color = new Color(0.04f, 0.07f, 0.1f, 0.85f);
            back.raycastTarget = false;

            var face = new GameObject("Face", typeof(RectTransform)).GetComponent<RectTransform>();
            face.SetParent(rect, false);
            face.anchorMin = Vector2.zero;
            face.anchorMax = Vector2.one;
            face.sizeDelta = new Vector2(-8f, -8f);
            var faceImage = face.gameObject.AddComponent<Image>();
            faceImage.preserveAspect = true;
            faceImage.raycastTarget = false;

            var edge = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            edge.SetParent(rect, false);
            edge.anchorMin = Vector2.zero;
            edge.anchorMax = Vector2.one;
            edge.sizeDelta = Vector2.zero;
            var edgeImage = edge.gameObject.AddComponent<Image>();
            edgeImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Members/HJH/05.Data/Resources/HJH/UI/panel_9slice.png");
            edgeImage.type = Image.Type.Sliced;
            edgeImage.fillCenter = false;
            edgeImage.color = new Color(1f, 1f, 1f, 0.9f);
            edgeImage.raycastTarget = false;

            var view = rect.gameObject.AddComponent<Assets.Members.HJH._02.Scripts.UI.PlayerPortraitHud>();
            var so = new SerializedObject(view);
            so.FindProperty("portrait").objectReferenceValue = faceImage;
            so.FindProperty("frame").objectReferenceValue = edgeImage;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("만듦 PlayerPortrait (얼굴 칸)");
            return rect;
        }

        private static void Walk(Transform parent, int depth, int max, StringBuilder text)
        {
            foreach (Transform child in parent)
            {
                if (child is RectTransform rt)
                {
                    string comps = string.Join(",", child.GetComponents<MonoBehaviour>().Where(m => m != null && !(m is Graphic) && !(m is LayoutGroup))
                        .Select(m => m.GetType().Name));
                    text.AppendLine($"{new string(' ', depth * 2)}{child.name}{(child.gameObject.activeSelf ? "" : " (off)")} [{comps}] aMin={rt.anchorMin} aMax={rt.anchorMax} piv={rt.pivot} pos={rt.anchoredPosition} size={rt.sizeDelta}");
                }
                if (depth < max)
                    Walk(child, depth + 1, max, text);
            }
        }

        private static string Path(Transform t, Transform root) =>
            t == root ? t.name : Path(t.parent, root) + "/" + t.name;
    }
}
