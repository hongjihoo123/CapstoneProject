using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    internal static class HudUiKit
    {
        public const string SpriteFolder = "Assets/Members/HJH/05.Data/Resources/HJH/UI/";
        public const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Pretendard-Medium SDF.asset";

        public static readonly Color Gold = new(0.84f, 0.72f, 0.47f, 1f);
        public static readonly Color Cyan = new(0.51f, 0.88f, 1f, 1f);
        public static readonly Color Title = new(0.91f, 0.94f, 0.97f, 1f);
        public static readonly Color Label = new(0.69f, 0.75f, 0.82f, 1f);
        public static readonly Color Value = new(0.93f, 0.96f, 0.98f, 1f);

        public static Sprite Load(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
#else
            return null;
#endif
        }

        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null || Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static TMP_FontAsset _font;
        public static TMP_FontAsset Font
        {
            get
            {
#if UNITY_EDITOR
                if (_font == null)
                    _font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
#endif
                return _font;
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
        {
            RectTransform rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, float spacing = 0f)
        {
            RectTransform rect = NewRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null)
                tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.characterSpacing = spacing;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static void BuildHeader(Transform parent, string title, float width)
        {
            Image eye = NewImage("Eye", parent, Load("eye_icon"), Color.white);
            Place(eye.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -9f), new Vector2(54f, 22f));
            TextMeshProUGUI text = NewText("Title", parent, title, 30f, Title, TextAlignmentOptions.MidlineLeft, 6f);
            Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(62f, 0f), new Vector2(width, 40f));
            Image line = NewImage("Line", parent, Load("divider"), new Color(1f, 1f, 1f, 0.55f));
            Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -30f), new Vector2(width, 14f));
        }
    }
}
