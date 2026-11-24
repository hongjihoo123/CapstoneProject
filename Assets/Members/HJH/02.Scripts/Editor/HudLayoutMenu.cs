using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HJH.EditorTools
{
    // PlayerRig HUD 배치 도구.
    //  - 진단: HUD 캔버스 자식들의 앵커·위치·크기를 Captures/_hud_dump.txt 로
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

        // 11-23 스케치 배치. 각 HUD 묶음의 앵커·피벗·위치만 바꿈 (안쪽 모양·글자는 그대로). 얼굴 칸이 없으면 하나 만듦.
        //  좌상단 콤보 목록 / 우상단 미니맵 / 우하단 스탯(Ctrl 펼치면 위로) / 좌하단 플레이어 상태(얼굴·체력·골드, 위에 버프) / 가운데 아래 스킬
        //  퀘스트는 스케치에 없어서 위 가운데 시간 밑으로
        // 주의: "코드 UI 굽기" 를 다시 돌리면 각 HUD 가 예전 위치로 돌아감 → 그 뒤에 이 메뉴를 다시 돌리면 됨
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

                // 좌하단: 플레이어 상태
                RectTransform hp = Find(root, "PlayerHpHud", log);
                Transform hud = hp != null ? hp.parent : null;
                RectTransform portrait = hud != null ? EnsurePortrait(hud, log) : null;
                Set(portrait, anchorLeftBottom, anchorLeftBottom, new Vector2(32f, 28f), log);
                Set(hp, anchorLeftBottom, anchorLeftBottom, new Vector2(160f, 82f), log);
                Set(Find(root, "GoldHud", log), anchorLeftBottom, anchorLeftBottom, new Vector2(160f, 28f), log);
                // 버프: 얼굴 칸 위. 피벗 아래 → 줄이 늘면 위로 자람 (긴 것 좌상단 순서는 그대로)
                Set(Find(root, "ComboBuffBar", log), anchorLeftBottom, anchorLeftBottom, new Vector2(32f, 156f), log);

                // 좌상단: 콤보 목록
                Set(Find(root, "ComboList", log), anchorLeftTop, anchorLeftTop, new Vector2(32f, -28f), log);

                // 우상단: 미니맵 (원 중심 기준이라 피벗 가운데)
                Set(Find(root, "MiniMap", log), anchorRightTop, new Vector2(0.5f, 0.5f), new Vector2(-135f, -135f), log);
                Set(Find(root, "MiniMapFrame", log), anchorRightTop, new Vector2(0.5f, 0.5f), new Vector2(-135f, -135f), log);

                // 우하단: 스탯. 피벗이 아래라 Ctrl 로 펼치면 위로 늘어남 (ContentSizeFitter)
                var anchorRightBottom = new Vector2(1f, 0f);
                Set(Find(root, "StatsMiniHud", log), anchorRightBottom, anchorRightBottom, new Vector2(-48f, 84f), log);

                // 퀘스트: 위 가운데 시간 밑
                Set(Find(root, "KillQuestHud", log), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), log);

                // 가운데 아래: 스킬 바 그대로, 패시브 칸은 스킬 줄 왼쪽 끝 (Q 왼쪽)
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

        // 위 배치로 옮기는 HUD 묶음 이름
        private static readonly string[] Managed =
        {
            "PlayerPortrait", "PlayerHpHud", "GoldHud", "ComboBuffBar", "ComboList", "MiniMap", "MiniMapFrame", "StatsMiniHud", "KillQuestHud", "Passive"
        };

        private static readonly string[] PositionProps = { "m_AnchoredPosition", "m_AnchorMin", "m_AnchorMax", "m_Pivot" };

        // 열린 씬의 PlayerRig 에서 위 HUD 묶음의 "위치·앵커·피벗" 덮어쓰기만 되돌림 → 프리팹 배치가 보이게.
        // 씬은 저장하지 않음 (저장은 직접)
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

        // 앵커·피벗을 바꾸고 위치를 줌. 크기는 그대로
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
            // 테두리 = 9-slice 판 그림 (가운데 비움). 다른 테두리를 쓰려면 Frame 의 Image 그림만 바꾸면 됨
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
