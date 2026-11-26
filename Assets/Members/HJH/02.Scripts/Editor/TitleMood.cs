using Assets.Members.HJH._02.Scripts.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace HJH.EditorTools
{
    internal static class TitleMood
    {
        private const float Left = 150f;

        public static void AddSettingsButton(MainMenuScreen menu, Transform canvas)
        {
            Transform page = canvas.Find("Menu");
            if (page == null)
                return;
            Transform start = page.Find("Start"), tutorial = page.Find("Tutorial"), quit = page.Find("Quit");
            if (start == null || tutorial == null || quit == null)
                return;

            Transform settings = page.Find("Settings");
            if (settings == null)
            {
                settings = Object.Instantiate(tutorial.gameObject, page).transform;
                settings.name = "Settings";
                settings.Find("Label").GetComponent<TMP_Text>().text = "설정";
                var btn = settings.GetComponent<Button>();
                ClearListeners(btn.onClick);
                UnityEventTools.AddPersistentListener(btn.onClick, menu.OpenSettings);
            }
            settings.SetSiblingIndex(tutorial.GetSiblingIndex() + 1);

            Transform[] order = { start, tutorial, settings, quit };
            var so = new SerializedObject(menu);
            SerializedProperty arr = so.FindProperty("menuButtons");
            arr.arraySize = order.Length;
            for (int i = 0; i < order.Length; i++)
            {
                var hover = order[i].GetComponent<HoverSelect>();
                ClearListeners(hover.OnEnter);
                UnityEventTools.AddIntPersistentListener(hover.OnEnter, menu.SetFocus, i);

                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = order[i].GetComponent<Button>();
                e.FindPropertyRelative("back").objectReferenceValue = order[i].GetComponent<Image>();
                e.FindPropertyRelative("label").objectReferenceValue = order[i].Find("Label").GetComponent<TMP_Text>();
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Apply(MainMenuScreen menu, Transform canvas)
        {
            Sprite hGrad = TitleDecorMenu.MakeSprite("title_hgrad", 256, 4, (x, y, w, h) =>
            {
                float u = (x + 0.5f) / w;
                return new Color(1f, 1f, 1f, Mathf.Pow(1f - u, 1.6f));
            });
            Sprite round = Pill("title_round", new Color(0.03f, 0.05f, 0.11f, 0.7f), new Color(0.8f, 0.88f, 1f, 0.45f), 2.5f, 128, 128);
            Sprite gear = TitleDecorMenu.MakeSprite("title_gear", 128, 128, GearPixel);
            SetBorder("title_round", 60);

            DressBackdrop(canvas.Find("Backdrop"), hGrad);
            LayoutTexts(canvas);
            LayoutButtons(menu, canvas);
            AddGear(menu, canvas, round, gear);
        }

        private static void DressBackdrop(Transform back, Sprite hGrad)
        {
            foreach (string n in new[] { "LeftShade" })
                Destroy(back, n);

            Image shade = TitleDecorMenu.NewImage("LeftShade", back, hGrad, new Color(0.01f, 0.02f, 0.07f, 0.88f));
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            shade.transform.SetSiblingIndex(2);

            Transform vig = back.Find("Vignette");
            if (vig != null && vig.TryGetComponent(out Image vi))
                vi.color = new Color(0.01f, 0.01f, 0.06f, 0.5f);
            Transform grad = back.Find("Gradient");
            if (grad != null && grad.TryGetComponent(out Image g))
                g.color = new Color(0f, 0.01f, 0.05f, 0.15f);
        }

        private static void LayoutTexts(Transform canvas)
        {
            foreach (string page in new[] { "Question", "Menu" })
            {
                Transform p = canvas.Find(page);
                if (p == null)
                    continue;
                Destroy(p, "Eye");
                Destroy(p, "SubLines");
                if (p.Find("Line") != null)
                    p.Find("Line").gameObject.SetActive(false);

                float size = page == "Menu" ? 88f : 72f;
                foreach (string n in new[] { "TitleShadow", "Title" })
                {
                    Transform t = p.Find(n);
                    if (t == null || !t.TryGetComponent(out TMP_Text text))
                        continue;
                    text.alignment = TextAlignmentOptions.MidlineLeft;
                    text.fontSize = size;
                    var rt = (RectTransform)t;
                    TitleDecorMenu.Place(rt, new Vector2(0f, 1f), new Vector2(Left, -110f - (n == "TitleShadow" ? 5f : 0f)), new Vector2(1200f, 130f));
                    text.colorGradient = new VertexGradient(Color.white, Color.white, new Color(0.8f, 0.86f, 1f), new Color(0.8f, 0.86f, 1f));
                    if (n == "TitleShadow")
                        text.color = new Color(0.02f, 0.02f, 0.12f, 0.8f);
                }

                Transform sub = p.Find("Sub");
                if (sub != null && sub.TryGetComponent(out TMP_Text subText))
                {
                    subText.alignment = TextAlignmentOptions.MidlineLeft;
                    subText.fontSize = 24f;
                    subText.color = page == "Menu" ? new Color(0.84f, 0.72f, 0.47f, 1f) : new Color(0.85f, 0.9f, 1f, 1f);
                    TitleDecorMenu.Place((RectTransform)sub, new Vector2(0f, 1f), new Vector2(Left + 6f, -232f), new Vector2(1000f, 36f));
                }
            }

            Transform done = canvas.Find("Menu/Done");
            if (done != null)
            {
                done.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                TitleDecorMenu.Place((RectTransform)done, new Vector2(0f, 0f), new Vector2(Left + 6f, 70f), new Vector2(400f, 28f));
            }
        }

        private static void LayoutButtons(MainMenuScreen menu, Transform canvas)
        {
            var so = new SerializedObject(menu);
            so.FindProperty("focusedSprite").objectReferenceValue = null;
            so.FindProperty("normalSprite").objectReferenceValue = null;
            so.FindProperty("focusedTint").colorValue = new Color(1f, 1f, 1f, 0f);
            so.FindProperty("normalTint").colorValue = new Color(1f, 1f, 1f, 0f);
            so.FindProperty("focusedLabel").colorValue = new Color(1f, 0.92f, 0.68f, 1f);
            so.FindProperty("normalLabel").colorValue = new Color(0.9f, 0.94f, 1f, 0.9f);

            foreach (string arrayName in new[] { "questionButtons", "menuButtons" })
            {
                SerializedProperty arr = so.FindProperty(arrayName);
                bool menuPage = arrayName == "menuButtons";
                for (int i = 0; i < arr.arraySize; i++)
                {
                    SerializedProperty e = arr.GetArrayElementAtIndex(i);
                    var back = (Image)e.FindPropertyRelative("back").objectReferenceValue;
                    var label = (TMP_Text)e.FindPropertyRelative("label").objectReferenceValue;
                    if (back == null)
                        continue;

                    back.sprite = null;
                    back.color = new Color(1f, 1f, 1f, 0f);
                    back.raycastTarget = true;
                    RectTransform rt = back.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(380f, 58f);
                    float y = menuPage ? 40f - i * 66f : 30f - i * 66f;
                    rt.anchoredPosition = new Vector2(Left, y);

                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    label.fontSize = menuPage && i == 0 ? 34f : 30f;
                    label.characterSpacing = 4f;
                    label.fontStyle = FontStyles.Normal;
                    var lrt = (RectTransform)label.transform;
                    lrt.anchorMin = Vector2.zero;
                    lrt.anchorMax = Vector2.one;
                    lrt.offsetMin = new Vector2(44f, 0f);
                    lrt.offsetMax = Vector2.zero;

                    Destroy(back.transform, "Marker");
                    Image marker = TitleDecorMenu.NewImage("Marker", back.transform, null, new Color(0.6f, 0.98f, 1f, 1f));
                    TitleDecorMenu.Place(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(11f, 11f));
                    marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    marker.enabled = i == 0;
                    e.FindPropertyRelative("marker").objectReferenceValue = marker;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddGear(MainMenuScreen menu, Transform canvas, Sprite round, Sprite gear)
        {
            Transform page = canvas.Find("Menu");
            if (page == null)
                return;
            Destroy(page, "GearButton");
            Image back = TitleDecorMenu.NewImage("GearButton", page, round, Color.white);
            back.type = Image.Type.Sliced;
            back.raycastTarget = true;
            TitleDecorMenu.Place(back.rectTransform, new Vector2(1f, 1f), new Vector2(-70f, -70f), new Vector2(64f, 64f));
            back.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image icon = TitleDecorMenu.NewImage("Icon", back.transform, gear, new Color(0.9f, 0.95f, 1f, 0.95f));
            TitleDecorMenu.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            var btn = back.gameObject.AddComponent<Button>();
            btn.targetGraphic = back;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.75f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.6f, 0.8f, 0.9f, 1f);
            btn.colors = colors;
            UnityEventTools.AddPersistentListener(btn.onClick, menu.OpenSettings);
        }

        private static Sprite Pill(string name, Color fill, Color outline, float outlineWidth, int w, int h) =>
            TitleDecorMenu.MakeSprite(name, w, h, (x, y, pw, ph) =>
            {
                float r = ph * 0.5f;
                float px = x + 0.5f, py = y + 0.5f;
                float cx = Mathf.Clamp(px, r, pw - r);
                float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - r) * (py - r)) - (r - 1f);
                float inside = Mathf.Clamp01(0.5f - d);
                float edge = Mathf.Clamp01(1f - Mathf.Abs(d + outlineWidth * 0.5f) / (outlineWidth * 0.5f + 0.5f));
                Color c = Color.Lerp(fill, outline, edge);
                c.a = Mathf.Max(fill.a * inside, outline.a * edge * inside);
                return c;
            });

        private static void SetBorder(string name, float border)
        {
            string path = $"Assets/Members/HJH/05.Data/UI/Title/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null || importer.spriteBorder.x == border)
                return;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }

        private static Color GearPixel(int x, int y, int w, int h)
        {
            float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            float angle = Mathf.Atan2(v, u);
            float tooth = Mathf.Clamp01(Mathf.Sin(angle * 8f) * 4f + 0.5f);
            float outer = 0.70f + 0.20f * tooth;
            float a = Mathf.Clamp01((outer - r) * 40f) * Mathf.Clamp01((r - 0.30f) * 40f);
            return new Color(1f, 1f, 1f, a);
        }

        private static void ClearListeners(UnityEngine.Events.UnityEventBase evt)
        {
            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(evt, i);
        }

        private static void Destroy(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            while (c != null)
            {
                Object.DestroyImmediate(c.gameObject);
                c = parent.Find(name);
            }
        }
    }
}
