using System.Collections.Generic;
using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public readonly struct SkillStat
    {
        public readonly string Label;
        public readonly string Value;

        public SkillStat(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    public abstract class SkillData : ScriptableObject
    {
        [SerializeField] private float cooldown;
        [SerializeField, Min(1), Tooltip("Casts that can be stored. Each one recharges over Cooldown.")]
        private int charges = 1;
        [SerializeField] private float duration;
        [SerializeField] private bool allowsMove = true;
        [SerializeField] private bool allowsFire = true;
        [SerializeField] private float moveSpeedMultiplier = 1f;
        [SerializeField] private bool cancelable;
        [SerializeField] private float cancelDelay;
        [SerializeField, Tooltip("Off for skills that should not add an element stack.")]
        private bool grantsElement = true;
        [SerializeField] private ElementType element;
        [SerializeField] private Sprite icon;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField, Tooltip("Effect color. Leave transparent to use the skill type default.")]
        private Color fxColor = Color.clear;

        public Sprite Icon => icon;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;
        public Color FxColor => fxColor.a > 0f ? fxColor : DefaultFxColor;
        protected virtual Color DefaultFxColor => new(1f, 0.7f, 0.3f);
        public float Cooldown => cooldown;
        public int Charges => Mathf.Max(1, charges);
        public float Duration => duration;
        public bool AllowsMove => allowsMove;
        public bool AllowsFire => allowsFire;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public bool Cancelable => cancelable;
        public float CancelDelay => cancelDelay;
        public virtual float CancelStartTime => cancelDelay;

        public bool TryGetElement(out ElementType result)
        {
            result = element;
            return grantsElement;
        }

        public virtual ISkillExecution Begin(ISkillContext context)
        {
            Execute(context);
            return null;
        }

        public virtual void Execute(ISkillContext context) { }

        public virtual void OnAnimationHitEvent(ISkillContext context) { }

        public virtual void DescribePreview(ISkillContext context, ISkillPreview preview) { }

        // Numbers shown in tooltips. Override to add skill specific ones (damage, range, ...) after base.
        public virtual void DescribeStats(List<SkillStat> stats)
        {
            if (cooldown > 0f)
                stats.Add(new SkillStat("재사용 대기시간", $"{cooldown:0.#}초"));
            if (Charges > 1)
                stats.Add(new SkillStat("충전", $"{Charges}회"));
            if (duration > 0.05f)
                stats.Add(new SkillStat("지속 시간", $"{duration:0.##}초"));
            if (!allowsMove)
                stats.Add(new SkillStat("이동", "불가"));
            else if (!Mathf.Approximately(moveSpeedMultiplier, 1f))
                stats.Add(new SkillStat("이동 속도", $"{moveSpeedMultiplier * 100f:0}%"));
        }
    }
}
