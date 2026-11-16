using Assets.Members.HJH._02.Scripts.UI;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Members.HJH.Test
{
    // 속성 링 셰이더 확인용 데모. 플레이하면 UI를 코드로 만들어서 위 줄(사용 전) / 아래 줄(Q 쓴 직후)을 보여줌.
    // 씬은 HJH/UI/속성 링 데모 씬 만들기 메뉴가 만든다. 게임 쪽 코드는 건드리지 않음.
    public class ElementRingDemo : MonoBehaviour
    {
        private struct Slot
        {
            public string key;
            public ElementType element;
            public float after;     // 아래 줄(Q 사용 직후)에서의 세기
            public float scale;     // 아래 줄에서 칸 크기 배율
        }

        private static readonly Slot[] Slots =
        {
            new Slot { key = "Q",     element = ElementType.Water,    after = 1.6f, scale = 1.00f },   // 방금 쓴 스킬: 번쩍
            new Slot { key = "E",     element = ElementType.Earth,    after = 1.9f, scale = 1.18f },   // 연계 (중간)
            new Slot { key = "RMB",   element = ElementType.Electric, after = 2.8f, scale = 1.48f },   // 연계 (강하게)
            new Slot { key = "R",     element = ElementType.Fire,     after = 1.7f, scale = 1.10f },   // 연계 (중간)
            new Slot { key = "SPACE", element = ElementType.Wind,     after = 2.8f, scale = 1.52f },   // 연계 (강하게)
        };

        private Font _font;

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Transform root = BuildCanvas();
            BuildRow(root, "BEFORE  -  nothing used yet", 190f, after: false);
            BuildRow(root, "AFTER  -  Q used  (RMB / SPACE = strong link, E / R = medium)", -190f, after: true);
        }

        private Transform BuildCanvas()
        {
            var go = new GameObject("DemoCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            bg.transform.SetParent(go.transform, false);
            var rect = (RectTransform)bg.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var tex = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                tex.SetPixel(0, y, Color.Lerp(new Color(0.08f, 0.10f, 0.17f), new Color(0.03f, 0.04f, 0.08f), y / 63f));
            tex.Apply();
            bg.GetComponent<RawImage>().texture = tex;
            return go.transform;
        }

        private void BuildRow(Transform root, string title, float y, bool after)
        {
            MakeText(root, title, new Vector2(0f, y + 190f), 30, new Color(0.85f, 0.9f, 0.97f, 1f), 900f);
            const float spacing = 300f;
            for (int i = 0; i < Slots.Length; i++)
            {
                Slot s = Slots[i];
                float scale = after ? s.scale : 1f;
                float intensity = after ? s.after : 1f;
                float size = 110f * scale;
                var pos = new Vector2((i - 2) * spacing, y);

                // 칸
                var disc = new GameObject("Slot_" + s.key, typeof(RectTransform), typeof(Image));
                disc.transform.SetParent(root, false);
                var dr = (RectTransform)disc.transform;
                dr.anchoredPosition = pos;
                dr.sizeDelta = Vector2.one * size;
                var img = disc.GetComponent<Image>();
                img.sprite = MakeDisc(s.element);
                img.raycastTarget = false;

                // 링
                var ring = new GameObject("Ring_" + s.key, typeof(RectTransform), typeof(Image), typeof(ElementRingView));
                ring.transform.SetParent(root, false);
                var rr = (RectTransform)ring.transform;
                rr.anchoredPosition = pos;
                rr.sizeDelta = Vector2.one * size * 1.9f;
                var view = ring.GetComponent<ElementRingView>();
                view.Setup(s.element, i * 1.7f + (after ? 0.3f : 0f));
                view.SetIntensity(intensity, snap: true);

                MakeText(root, s.key, pos + new Vector2(0f, -(size * 0.5f + 34f)), 26, new Color(0.8f, 0.85f, 0.92f, 1f), 200f);
                if (after && s.key == "E")
                    MakeText(root, "NEXT", pos + new Vector2(0f, size * 0.5f + 52f), 26, new Color(1f, 0.9f, 0.55f, 1f), 200f);
            }
        }

        private void MakeText(Transform root, string text, Vector2 pos, int size, Color color, float width)
        {
            var go = new GameObject("Text_" + text, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(root, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(width, 40f);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
        }

        // 아이콘 대신 쓰는 속성 색 어두운 원판
        private static Sprite MakeDisc(ElementType element)
        {
            Color baseColor = element switch
            {
                ElementType.Fire => new Color(0.55f, 0.16f, 0.08f),
                ElementType.Water => new Color(0.08f, 0.32f, 0.55f),
                ElementType.Wind => new Color(0.10f, 0.45f, 0.25f),
                ElementType.Electric => new Color(0.5f, 0.42f, 0.08f),
                _ => new Color(0.42f, 0.27f, 0.12f)
            };
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - r) * n * 0.5f);
                    Color c = Color.Lerp(baseColor * 1.35f, baseColor * 0.35f, Mathf.Clamp01(r * 0.9f + dy * 0.2f));
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
