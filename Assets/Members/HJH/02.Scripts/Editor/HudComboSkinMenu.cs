using Assets.Members.HJH._02.Scripts.UI;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HJH.EditorTools
{
    // 콤보 쪽 UI(원소 칸 4개, 콤보 리스트)를 새 디자인으로. 스크립트 동작은 안 건드리고 그림/색만 바꿈.
    //  - 원소 칸 틀: 가시 달린 원 -> 얇은 원형 프레임 + 어두운 원판
    //  - 콤보 리스트: 뒤에 박스 대신 부드러운 어둠, 줄 배경은 왼쪽에서 번지는 청록 빛, 단계 칸은 원판
    public static class HudComboSkinMenu
    {
        private const string PrefabPath = "Assets/Members/HJH/05.Data/Prefab/PlayerRig.prefab";
        private const string Dir = "Assets/Members/HJH/05.Data/Resources/HJH/UI/";

        [MenuItem("HJH/UI/콤보 UI 새 디자인 적용")]
        public static void Apply()
        {
            var frame = Load("slot_circle_frame");
            var disc = Load("slot_circle_bg");
            var glow = Load("soft_glow");
            var rowBar = Load("button_hover");
            if (frame == null || disc == null || glow == null || rowBar == null)
            {
                Debug.LogWarning("[ComboSkin] 스프라이트 없음. 먼저 HJH/UI/HUD 에셋 가져오기 실행");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                int slots = 0;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Element Slot UI") && t.TryGetComponent(out Image img))
                    {
                        img.sprite = frame;
                        img.type = Image.Type.Simple;
                        img.preserveAspect = true;
                        if (t.Find("SlotBack") == null)
                        {
                            Image back = NewImage("SlotBack", t, disc);
                            back.rectTransform.SetAsFirstSibling();
                            Stretch(back.rectTransform, 12f);
                        }
                        slots++;
                    }

                    // 원소 아이콘: 예전 틀 기준으로 아래로 5 내려가 있던 걸 가운데로, 크기는 원 안쪽에 맞춤
                    if (t.name == "Slot Image" && t.parent != null && t.parent.name.StartsWith("Element Slot UI"))
                    {
                        var ir = (RectTransform)t;
                        ir.anchoredPosition = Vector2.zero;
                        ir.sizeDelta = new Vector2(66f, 66f);
                    }
                }

                // 원소 칸이 톡 커졌다 돌아오는 연출을 짧고 작게 (예전: 0.25배, 0.3초 + 흔들림)
                ElementStackView stackView = root.GetComponentInChildren<ElementStackView>(true);
                if (stackView != null)
                {
                    var stackSo = new SerializedObject(stackView);
                    stackSo.FindProperty("bounceScale").floatValue = 0.14f;
                    stackSo.FindProperty("bounceDuration").floatValue = 0.16f;
                    stackSo.ApplyModifiedPropertiesWithoutUndo();
                }

                Transform list = Find(root, "ComboList");
                if (list != null)
                {
                    if (list.Find("SoftGlow") == null)
                    {
                        Image g = NewImage("SoftGlow", list, glow);
                        g.rectTransform.SetAsFirstSibling();
                        g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        g.rectTransform.sizeDelta = new Vector2(640f, 520f);
                        g.rectTransform.anchoredPosition = new Vector2(-40f, 20f);
                    }

                    Transform template = Find(list, "RowTemplate");
                    if (template != null)
                    {
                        if (template.TryGetComponent(out Image rowImg))
                        {
                            rowImg.sprite = rowBar;
                            rowImg.type = Image.Type.Simple;
                        }
                        Transform step = template.Find("Steps") != null && template.Find("Steps").childCount > 0 ? template.Find("Steps").GetChild(0) : null;
                        if (step != null && step.TryGetComponent(out Image stepImg))
                            stepImg.sprite = disc;
                    }
                }

                // 줄 색: 금색 갈색 -> 얇은 청록 (완성 때 번쩍은 그대로 금색)
                ComboUI combo = root.GetComponentInChildren<ComboUI>(true);
                if (combo != null)
                {
                    var so = new SerializedObject(combo);
                    SerializedProperty p = so.FindProperty("listRowColor");
                    if (p != null)
                    {
                        p.colorValue = new Color(0.55f, 0.82f, 1f, 0.55f);
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[ComboSkin] 완료 (원소 칸 {slots}개, 리스트 {(list != null ? "O" : "X")})");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Dir + name + ".png");

        private static Transform Find(GameObject root, string name) => Find(root.transform, name);

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
