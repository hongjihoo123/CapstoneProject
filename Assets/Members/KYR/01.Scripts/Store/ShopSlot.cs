using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01.Scripts.Currency;
using TMPro;
using UnityEngine;

namespace Members.KYR._01.Scripts.Store
{
    public class ShopSlot : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private const int CircleSegments = 48;

        [SerializeField] private ElementPalette palette;
        [SerializeField] private Transform billboard;
        [SerializeField] private Renderer icon;
        [SerializeField] private Renderer glow;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Light glowLight;
        [SerializeField] private float glowIntensity = 0.75f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float highlightScale = 1.18f;
        [SerializeField, Range(0.3f, 0.5f)] private float iconCircle = 0.47f;

        private MaterialPropertyBlock _block;
        private Vector3 _billboardRest;
        private Vector3 _billboardScale;
        private float _scale = 1f;
        private bool _highlighted;
        private string _prompt;
        private Mesh _disc;

        public SkillData Skill { get; private set; }
        public int Price { get; private set; }

        private void Awake()
        {
            if (billboard != null)
            {
                _billboardRest = billboard.localPosition;
                _billboardScale = billboard.localScale;
            }

            if (label == null)
                return;

            label.alignment = TextAlignmentOptions.Center;
            label.richText = true;
            label.overflowMode = TextOverflowModes.Overflow;
            RectTransform rect = label.rectTransform;
            rect.sizeDelta = new Vector2(8f, 5.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 1.55f);
        }
        
        public void Show(SkillData skill, int price)
        {
            Skill = skill;
            Price = Mathf.Max(0, price);
            RefreshVisual();
            RefreshLabel();
        }

        public void SetHighlighted(bool highlighted, string prompt)
        {
            if (_highlighted == highlighted && _prompt == prompt)
                return;

            _highlighted = highlighted;
            _prompt = prompt;
            RefreshLabel();
        }

        public void RefreshLabel()
        {
            if (label == null)
                return;

            if (Skill == null)
            {
                label.text = string.Empty;
                return;
            }

            string priceHex = ColorUtility.ToHtmlStringRGB(CanAfford(Price) ? Shop.AffordableColor : Shop.ShortColor);
            string text = $"{Skill.DisplayName}\n<size=68%><color=#{priceHex}>{Price}</color></size>";
            if (!string.IsNullOrEmpty(_prompt))
                text += $"\n<size=58%><color=#C9CCD6>{_prompt}</color></size>";
            label.text = text;
        }

        private void LateUpdate()
        {
            float target = _highlighted ? highlightScale : 1f;
            _scale = Mathf.MoveTowards(_scale, target, Time.deltaTime * 2f);

            Camera cam = Camera.main;
            if (billboard != null)
            {
                billboard.localPosition = _billboardRest + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.03f;
                billboard.localScale = _billboardScale * (_scale * pulse);
                if (cam != null)
                    billboard.rotation = cam.transform.rotation;
            }

            if (label != null && cam != null)
                label.transform.rotation = cam.transform.rotation;
        }

        private void OnDestroy()
        {
            if (_disc != null)
                Destroy(_disc);
        }

        private void RefreshVisual()
        {
            Color color = ElementColor();
            Tint(glow, color * glowIntensity);
            if (glowLight != null)
                glowLight.color = color;

            if (icon == null)
                return;

            _block ??= new MaterialPropertyBlock();
            icon.GetPropertyBlock(_block);
            _block.SetColor(TintId, Color.white);
            Texture texture = Skill != null && Skill.Icon != null ? Skill.Icon.texture : null;
            if (texture != null)
                _block.SetTexture(MainTexId, texture);
            icon.SetPropertyBlock(_block);
            icon.enabled = texture != null;
            if (texture != null)
                FitIconToSprite(Skill.Icon);
        }

        private Color ElementColor()
        {
            ElementPalette colors = palette != null ? palette : ElementPalette.Default;
            if (Skill != null && Skill.TryGetElement(out ElementType element)
                && colors != null && colors.TryGet(element, out ElementPalette.Entry entry))
                return entry.color;

            return colors != null ? colors.NeutralColor : Color.gray;
        }

        private void FitIconToSprite(Sprite sprite)
        {
            if (!icon.TryGetComponent(out MeshFilter filter))
                return;

            Rect rect = sprite.textureRect;
            float width = sprite.texture.width;
            float height = sprite.texture.height;
            var uvCenter = new Vector2(rect.center.x / width, rect.center.y / height);
            var uvSize = new Vector2(rect.width / width, rect.height / height);

            var vertices = new Vector3[CircleSegments + 1];
            var uvs = new Vector2[CircleSegments + 1];
            var colors = new Color[CircleSegments + 1];
            var triangles = new int[CircleSegments * 3];

            vertices[0] = Vector3.zero;
            uvs[0] = uvCenter;
            colors[0] = Color.white;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices[i + 1] = new Vector3(dir.x, dir.y, 0f) * 0.5f;
                uvs[i + 1] = uvCenter + Vector2.Scale(dir * iconCircle, uvSize);
                colors[i + 1] = Color.white;

                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % CircleSegments + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            if (_disc == null)
                _disc = new Mesh { name = "ShopSlotIconDisc" };
            _disc.Clear();
            _disc.vertices = vertices;
            _disc.uv = uvs;
            _disc.colors = colors;
            _disc.triangles = triangles;
            _disc.RecalculateBounds();
            filter.sharedMesh = _disc;
        }

        private void Tint(Renderer target, Color color)
        {
            if (target == null)
                return;

            _block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(_block);
            _block.SetColor(TintId, color);
            target.SetPropertyBlock(_block);
        }

        private static bool CanAfford(int price)
        {
            if (price <= 0)
                return true;

            CurrencyService currency = GameBootstrap.Currency;
            return currency != null && currency.CanAfford(CurrencyType.RunGold, price);
        }
    }
}
