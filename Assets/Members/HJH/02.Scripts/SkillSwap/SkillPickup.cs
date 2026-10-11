using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Assets.Members.HJH._02.Scripts.Element;
using Members.JJH._02_Scripts.ElementsSystem;
using TMPro;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.SkillSwap
{
    // A skill lying in the stage (Shape of Dreams "memory"): the skill icon in a soft element-colored glow,
    // one flat billboard that always faces the camera, plus a small point light on the floor. SkillSwapInteractor trades it with one of the player's Q/E skills,
    // so after a swap it holds the player's old skill (and that skill's cooldown keeps running here).
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

        // Puts the incoming skill (with its cooldown) on the ground and hands back what was here.
        // An empty incoming skill (the slot had nothing) uses the pickup up.
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

        // The label only shows while highlighted with a prompt (Shape of Dreams keeps the floor quiet);
        // a null prompt hides it, e.g. while the swap box is open over the orb.
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
            }
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

            // Name only; the element is already the orb's color and the details live in the swap box.
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

            // Billboard: the label faces the top-down camera.
            if (label != null && cam != null)
                label.transform.rotation = cam.transform.rotation;
        }
    }
}
