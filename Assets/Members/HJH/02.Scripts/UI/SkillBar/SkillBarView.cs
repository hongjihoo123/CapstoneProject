using Assets.Members.HJH._02.Scripts.Hud;
using Assets.Members.HJH._02.Scripts.Element;
using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 아래 스킬바임. 패시브 + 슬롯마다 SkillSlotView 하나씩
    // 매 프레임 스킬 상태 읽어서 그려서, 스킬이나 캐릭터 바뀌어도 알아서 따라감
    public class SkillBarView : MonoBehaviour
    {
        [Serializable]
        public struct SlotBinding
        {
            public SkillSlotId slot;
            public SkillSlotView view;
            [Tooltip("Drawn instead of the key text (e.g. a Space bar cap). Optional.")]
            public Sprite keyIcon;
        }

        [SerializeField] private PlayerAgent player;
        [SerializeField] private ElementPalette palette;
        [SerializeField] private SlotBinding[] slots;
        [SerializeField] private Image passiveIcon;

        [Header("콤보 연계 강조 (속성 링 세기 / 칸 크기)")]
        [SerializeField] private float idleIntensity = 1f;
        [SerializeField, Tooltip("Combo chain is running: slots that are not the next step calm down.")]
        private float chainOtherIntensity = 0.6f;
        [SerializeField, Tooltip("The skill that was just used.")]
        private float lastUsedIntensity = 1.5f;
        [SerializeField, Tooltip("The skill that continues the combo: use this one now.")]
        private float nextIntensity = 2.8f;
        [SerializeField] private float nextScale = 1.18f;
        [SerializeField, Tooltip("Emphasis shrinks back during the last seconds of the combo window.")]
        private float emphasisFadeTime = 0.8f;

        public bool TryGetView(SkillSlotId slot, out SkillSlotView view)
        {
            foreach (SlotBinding binding in slots)
            {
                if (binding.slot == slot && binding.view != null)
                {
                    view = binding.view;
                    return true;
                }
            }

            view = null;
            return false;
        }

        public IEnumerable<SlotBinding> Bindings => slots;

        private SkillStateModule _subscribed;

        private void Start()
        {
            if (player == null)
                return;

            _subscribed = player.SkillFsm;
            if (_subscribed != null)
            {
                _subscribed.SkillDenied += HandleSkillDenied;
                _subscribed.SkillReady += HandleSkillReady;
            }

            _hud = PlayerHudSource.Instance;
            if (_hud != null)
                _hud.ComboBroken += HandleComboBroken;

            foreach (SlotBinding binding in slots)
            {
                if (binding.view != null)
                {
                    if (binding.keyIcon != null)
                        binding.view.SetKeyIcon(binding.keyIcon);
                    else
                        binding.view.SetKeyLabel(player.GetSkillKeyLabel(binding.slot));
                }
            }
        }

        private void OnDestroy()
        {
            if (_subscribed != null)
            {
                _subscribed.SkillDenied -= HandleSkillDenied;
                _subscribed.SkillReady -= HandleSkillReady;
            }

            if (_hud != null)
                _hud.ComboBroken -= HandleComboBroken;
        }

        private PlayerHudSource _hud;

        private void HandleSkillDenied(SkillSlotId slot)
        {
            if (TryGetView(slot, out SkillSlotView view))
                view.Deny();
        }

        private void HandleSkillReady(SkillSlotId slot)
        {
            if (TryGetView(slot, out SkillSlotView view))
                view.PlayReady();
        }

        private void LateUpdate()
        {
            SkillStateModule skills = player != null ? player.SkillFsm : null;
            if (skills == null)
                return;

            foreach (SlotBinding binding in slots)
            {
                if (binding.view == null)
                    continue;

                binding.view.Bind(skills.GetSkill(binding.slot), palette);

                float remaining = skills.GetCooldownRemaining(binding.slot);
                int charges = skills.GetCharges(binding.slot);
                float lockLeft = skills.GetLockRemaining(binding.slot);
                // 교체 직후 잠금이 실제로 막고 있을 땐 그것도 쿨타임처럼 보여줌
                if (lockLeft > 0f && (charges > 0 || lockLeft >= remaining))
                    binding.view.SetCooldown(lockLeft, skills.GetLockDuration(binding.slot), 0, 1);
                else
                    binding.view.SetCooldown(remaining, skills.GetCooldownDuration(binding.slot), charges, skills.GetMaxCharges(binding.slot));
            }

            UpdateComboEmphasis(skills);

            if (passiveIcon != null)
            {
                Sprite sprite = skills.Passive != null ? skills.Passive.Icon : null;
                passiveIcon.sprite = sprite;
                passiveIcon.enabled = sprite != null;
                DrawPassiveGauge(sprite);
            }
        }

        // 콤보 진행 상황을 읽어서 슬롯마다 링 세기/크기를 정함
        //  평소: 모두 기본 / 콤보 중: 방금 쓴 스킬은 번쩍, 이어지는(NEXT) 속성의 스킬은 크고 세게, 나머지는 잠잠
        private void UpdateComboEmphasis(SkillStateModule skills)
        {
            PlayerHudSource hud = PlayerHudSource.Instance;
            bool chain = false;
            bool hasLast = false;
            SkillSlotId lastSlot = default;
            bool hasNext = false;
            ElementType next = default;

            // 콤보 시간이 끝나 가면 강조도 같이 줄어서, 시간이 다 되는 순간 평소로 돌아와 있게 함
            float fade = 1f;
            if (hud != null)
            {
                IReadOnlyList<ElementComboChain.ComboInput> inputs = hud.ComboInputs;
                chain = inputs.Count > 0;
                fade = Mathf.Clamp01(hud.ComboTimeLeft / Mathf.Max(0.05f, emphasisFadeTime));
                if (chain)
                {
                    lastSlot = inputs[inputs.Count - 1].Slot;
                    hasLast = true;
                }

                hasNext = hud.TryGetNextCombo(out next, out _, out _);
            }

            foreach (SlotBinding binding in slots)
            {
                if (binding.view == null)
                    continue;

                float intensity = idleIntensity;
                float scale = 1f;
                if (chain)
                {
                    intensity = chainOtherIntensity;
                    if (hasLast && binding.slot == lastSlot)
                        intensity = lastUsedIntensity;

                    SkillData skill = skills.GetSkill(binding.slot);
                    if (hasNext && skill != null && skill.TryGetElement(out ElementType element) && element == next)
                    {
                        intensity = nextIntensity;
                        scale = nextScale;
                    }
                }

                if (chain)
                {
                    intensity = Mathf.Lerp(idleIntensity, intensity, fade);
                    scale = Mathf.Lerp(1f, scale, fade);
                }

                // 콤보가 끊긴 프레임에는 서서히가 아니라 바로 평소 상태로
                binding.view.SetComboEmphasis(intensity, scale, snap: _snapIdle && !chain);
            }

            _snapIdle = false;
        }

        private bool _snapIdle;
        private void HandleComboBroken() => _snapIdle = true;

        // 게이지 있는 패시브(건슬링어 충전 충격포 등)면 패시브 칸이 아래서부터 차오름.
        // 뒤에 흐린 아이콘 하나 깔고, 앞 아이콘을 게이지만큼만 보여주는 방식. 꽉 차면 금색으로 깜빡임
        private Image _passiveBack;

        private void DrawPassiveGauge(Sprite sprite)
        {
            PlayerHudSource hud = PlayerHudSource.Instance;
            float fill = 0f;
            bool full = false;
            bool hasGauge = hud != null && hud.TryGetPassiveGauge(out fill, out full);

            if (!hasGauge)
            {
                passiveIcon.type = Image.Type.Simple;
                passiveIcon.color = Color.white;
                if (_passiveBack != null)
                    _passiveBack.enabled = false;
                return;
            }

            if (_passiveBack == null)
            {
                var back = new GameObject("PassiveGaugeBack", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)back.transform;
                RectTransform icon = passiveIcon.rectTransform;
                rect.SetParent(icon.parent, false);
                rect.anchorMin = icon.anchorMin;
                rect.anchorMax = icon.anchorMax;
                rect.pivot = icon.pivot;
                rect.anchoredPosition = icon.anchoredPosition;
                rect.sizeDelta = icon.sizeDelta;
                rect.SetSiblingIndex(icon.GetSiblingIndex());   // 아이콘 바로 뒤
                _passiveBack = back.GetComponent<Image>();
                _passiveBack.raycastTarget = false;
                _passiveBack.preserveAspect = passiveIcon.preserveAspect;
            }

            _passiveBack.sprite = sprite;
            _passiveBack.enabled = sprite != null;
            _passiveBack.color = new Color(1f, 1f, 1f, 0.25f);

            passiveIcon.type = Image.Type.Filled;
            passiveIcon.fillMethod = Image.FillMethod.Vertical;
            passiveIcon.fillOrigin = (int)Image.OriginVertical.Bottom;
            passiveIcon.fillAmount = full ? 1f : fill;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            passiveIcon.color = full ? Color.Lerp(Color.white, new Color(1f, 0.83f, 0.3f), pulse) : Color.white;
        }
    }
}
