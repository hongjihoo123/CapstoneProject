using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using Members.KYR._01.Scripts.Stats;
using RobotWeapons;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    public enum HitEffectKind
    {
        Damage,
        Burn,
        Slow,
        Root,
        Stun,
        Knockback
    }

    [Serializable]
    public struct HitEffect
    {
        public HitEffectKind kind;
        public float value;
        public float duration;
    }

    public enum SelfEffectKind
    {
        Heal,
        Defense,
        MoveSpeed,
        AttackSpeed,
        Damage,
        CooldownSpeed,
        DashCharge,
        Vulnerability,
        Shield
    }

    [Serializable]
    public struct SelfEffect
    {
        public SelfEffectKind kind;
        public float value;
        public float duration;
    }

    public static class SkillEffects
    {
        private const float BurnTick = 0.5f;

        public static void Apply(ISkillContext context, SkillData skill, IReadOnlyList<HitEffect> effects,
            IDamageable target, Vector3 push)
        {
            ICrowdControlTarget cc = null;
            float damageUp = SkillUpgrades.Multiplier(SkillUpgradeKind.Damage, skill);
            float statusUp = SkillUpgrades.Multiplier(SkillUpgradeKind.StatusPower, skill);
            foreach (HitEffect effect in effects)
            {
                if (target == null || !target.IsAlive)
                    return;

                switch (effect.kind)
                {
                    case HitEffectKind.Damage:
                        context.DealDamage(target, effect.value, skill);
                        break;
                    case HitEffectKind.Burn:
                        context.ApplyDamageOverTime(target, effect.value * damageUp * BurnTick, BurnTick, effect.duration * statusUp);
                        break;
                    case HitEffectKind.Slow:
                        (cc ??= CrowdControl.Of(target))?.ApplySlow(effect.value, effect.duration * statusUp);
                        break;
                    case HitEffectKind.Root:
                        (cc ??= CrowdControl.Of(target))?.ApplyRoot(effect.duration * statusUp);
                        break;
                    case HitEffectKind.Stun:
                        (cc ??= CrowdControl.Of(target))?.ApplyStun(effect.duration * statusUp);
                        break;
                    case HitEffectKind.Knockback:
                        (cc ??= CrowdControl.Of(target))?.ApplyKnockback(push, effect.value * statusUp);
                        break;
                }
            }
        }

        public static void ApplySelf(ISkillContext context, IReadOnlyList<SelfEffect> effects, object source)
        {
            float supportUp = SkillUpgrades.Multiplier(SkillUpgradeKind.Support, (source as ElementSkillCast)?.Skill);
            foreach (SelfEffect effect in effects)
            {
                switch (effect.kind)
                {
                    case SelfEffectKind.Heal:
                        context.Heal(effect.value * supportUp);
                        break;
                    case SelfEffectKind.Defense:
                        AddBuff(context, PlayerStatId.DamageReduction, StatModifierType.Flat, new SelfEffect { kind = effect.kind, value = effect.value * supportUp, duration = effect.duration }, source);
                        break;
                    case SelfEffectKind.MoveSpeed:
                        AddBuff(context, PlayerStatId.MoveSpeed, StatModifierType.PercentAdd, effect, source);
                        break;
                    case SelfEffectKind.AttackSpeed:
                        AddBuff(context, PlayerStatId.AttackSpeed, StatModifierType.PercentAdd, effect, source);
                        break;
                    case SelfEffectKind.Damage:
                        AddBuff(context, PlayerStatId.Damage, StatModifierType.PercentAdd, effect, source);
                        break;
                    case SelfEffectKind.CooldownSpeed:
                        context.Run(new CooldownBoost(context, effect.value / 100f, effect.duration));
                        break;
                    case SelfEffectKind.DashCharge:
                        for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(effect.value)); i++)
                            context.ReduceCooldown(SkillSlotId.Dash, context.GetCooldownRemaining(SkillSlotId.Dash) + 0.01f);
                        break;
                    case SelfEffectKind.Shield:
                        context.AddShield(effect.value * supportUp);
                        break;
                    case SelfEffectKind.Vulnerability:
                        AddBuff(context, PlayerStatId.IncomingDamageTaken, StatModifierType.PercentMore, effect, source);
                        break;
                }
            }
        }

        private static void AddBuff(ISkillContext context, PlayerStatId stat, StatModifierType type, SelfEffect effect, object source)
        {
            if (!context.StatsReady)
                return;
            context.AddStatModifier(stat, new StatModifier(source, type, effect.value / 100f, effect.duration));
        }

        public static void Describe(List<SkillStat> stats, IReadOnlyList<HitEffect> hits, IReadOnlyList<SelfEffect> self)
        {
            foreach (HitEffect e in hits)
            {
                stats.Add(e.kind switch
                {
                    HitEffectKind.Damage => new SkillStat("피해량", $"{e.value:0}"),
                    HitEffectKind.Burn => new SkillStat("화상", $"초당 {e.value:0} · {e.duration:0.#}초"),
                    HitEffectKind.Slow => new SkillStat("둔화", $"{e.value:0}% · {e.duration:0.#}초"),
                    HitEffectKind.Root => new SkillStat("속박", $"{e.duration:0.#}초"),
                    HitEffectKind.Stun => new SkillStat("기절", $"{e.duration:0.#}초"),
                    HitEffectKind.Knockback => new SkillStat("밀치기", $"{e.value:0.#}m"),
                    _ => default
                });
            }

            foreach (SelfEffect e in self)
            {
                stats.Add(e.kind switch
                {
                    SelfEffectKind.Heal => new SkillStat("회복", $"{e.value:0}"),
                    SelfEffectKind.Defense => new SkillStat("받는 피해 감소", $"{e.value:0}% · {e.duration:0.#}초"),
                    SelfEffectKind.MoveSpeed => new SkillStat("이동 속도", $"+{e.value:0}% · {e.duration:0.#}초"),
                    SelfEffectKind.AttackSpeed => new SkillStat("공격 속도", $"+{e.value:0}% · {e.duration:0.#}초"),
                    SelfEffectKind.Damage => new SkillStat("공격력", $"+{e.value:0}% · {e.duration:0.#}초"),
                    SelfEffectKind.CooldownSpeed => new SkillStat("쿨타임 가속", $"+{e.value:0}% · {e.duration:0.#}초"),
                    SelfEffectKind.DashCharge => new SkillStat("대쉬 충전", $"{e.value:0}회"),
                    SelfEffectKind.Shield => new SkillStat("쉴드", $"{e.value:0}"),
                    SelfEffectKind.Vulnerability => new SkillStat("받는 피해", $"+{e.value:0}% · {e.duration:0.#}초"),
                    _ => default
                });
            }
        }

        private sealed class CooldownBoost : ISkillTimedEffect
        {
            private readonly ISkillContext _context;
            private readonly float _rate;
            private float _left;

            public CooldownBoost(ISkillContext context, float rate, float duration)
            {
                _context = context;
                _rate = rate;
                _left = duration;
            }

            public bool Tick(float deltaTime)
            {
                foreach (SkillSlotId slot in SkillSlots.All)
                    _context.ReduceCooldown(slot, deltaTime * _rate);
                _left -= deltaTime;
                return _left > 0f;
            }
        }
    }
}
