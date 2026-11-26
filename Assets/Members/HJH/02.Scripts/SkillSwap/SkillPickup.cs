using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.SkillSwap
{
    public class SkillPickup : MonoBehaviour
    {
        private static readonly List<SkillPickup> Active = new();
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [SerializeField] private SkillData skill;
        [SerializeField] private ElementPalette palette;

        [Header("Visual")]
        [SerializeField, Tooltip("Faces the camera every frame (glow, icon).")]
        private Transform billboard;
        [SerializeField] private Renderer icon;
        [SerializeField] private Renderer glow;
        [SerializeField] private TMP_Text label;
        [SerializeField, Tooltip("Optional point light tinted with the element.")] private Light glowLight;
        [SerializeField] private float glowIntensity = 1f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float highlightScale = 1.18f;

        private MaterialPropertyBlock _block;
        private Vector3 _billboardRest;
        private Vector3 _billboardScale;
        private float _scale = 1f;
        private bool _highlighted;
        private string _prompt;

        public static IReadOnlyList<SkillPickup> All => Active;
        public SkillData Skill => skill;
        public SkillCooldownState Cooldown { get; private set; }

        private void Awake()
        {
            if (billboard != null)
            {
                _billboardRest = billboard.localPosition;
                _billboardScale = billboard.localScale;
            }

            if (label != null)
                label.alpha = 0f;
        }

        private void OnEnable()
        {
            Active.Add(this);
            Refresh();
        }

        private void OnDisable() => Active.Remove(this);

        public SkillData Swap(SkillData incoming, SkillCooldownState incomingCooldown, out SkillCooldownState outgoingCooldown)
        {
            SkillData outgoing = skill;
            outgoingCooldown = Cooldown;
            skill = incoming;
            Cooldown = incomingCooldown;
            _prompt = null;
            Refresh();

            if (skill == null)
                gameObject.SetActive(false);

            return outgoing;
        }

        public void SetHighlighted(bool highlighted, string prompt)
        {
            _highlighted = highlighted;
            if (_prompt == prompt)
                return;

            _prompt = prompt;
            RefreshLabel();
        }

        public static string ElementLabel(SkillData data) =>
            data != null && data.TryGetElement(out ElementType element)
                ? $"{ElementComboBook.KoreanName(element)} 속성"
                : "무속성";

        public Color ElementColor()
        {
            ElementPalette colors = palette != null ? palette : ElementPalette.Default;
            if (skill != null && skill.TryGetElement(out ElementType element)
                && colors != null && colors.TryGet(element, out ElementPalette.Entry entry))
                return entry.color;

            return colors != null ? colors.NeutralColor : Color.gray;
        }

        private void Refresh()
        {
            RefreshLabel();

            Color color = ElementColor();
            Tint(glow, color * glowIntensity);
            if (glowLight != null)
                glowLight.color = color;

            if (icon != null)
            {
                _block ??= new MaterialPropertyBlock();
                icon.GetPropertyBlock(_block);
                _block.SetColor(TintId, Color.white);
                Texture texture = skill != null && skill.Icon != null ? skill.Icon.texture : null;
                if (texture != null)
                    _block.SetTexture(MainTexId, texture);
                icon.SetPropertyBlock(_block);
                icon.enabled = texture != null;
                if (texture != null)
                    FitIconToSprite(skill.Icon);
            }
        }

        [SerializeField, Range(0.3f, 0.5f), Tooltip("그림에서 잘라 쓸 원의 반지름 (0.5 = 사각형에 딱 맞는 원)")]
        private float iconCircle = 0.47f;

        private const int CircleSegments = 48;
        private Mesh _disc;

        private void FitIconToSprite(Sprite sprite)
        {
            if (!icon.TryGetComponent(out MeshFilter filter))
                return;

            Rect r = sprite.textureRect;
            float w = sprite.texture.width;
            float h = sprite.texture.height;
            var uvCenter = new Vector2(r.center.x / w, r.center.y / h);
            var uvSize = new Vector2(r.width / w, r.height / h);

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
                _disc = new Mesh { name = "PickupIconDisc" };
            _disc.Clear();
            _disc.vertices = vertices;
            _disc.uv = uvs;
            _disc.colors = colors;
            _disc.triangles = triangles;
            _disc.RecalculateBounds();
            filter.sharedMesh = _disc;
        }

        private void OnDestroy()
        {
            if (_disc != null)
                Destroy(_disc);
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

        private void RefreshLabel()
        {
            if (label == null)
                return;

            if (skill == null)
            {
                label.text = string.Empty;
                return;
            }

            string text = skill.DisplayName;
            if (!string.IsNullOrEmpty(_prompt))
                text += $"\n<size=68%><color=#C9CCD6>{_prompt}</color></size>";
            label.text = text;
        }

        private void LateUpdate()
        {
            float target = _highlighted ? highlightScale : 1f;
            _scale = Mathf.MoveTowards(_scale, target, Time.deltaTime * 2f);

            if (label != null)
            {
                bool show = _highlighted && !string.IsNullOrEmpty(_prompt);
                label.alpha = Mathf.MoveTowards(label.alpha, show ? 1f : 0f, Time.deltaTime * 6f);
            }

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
    }
}
