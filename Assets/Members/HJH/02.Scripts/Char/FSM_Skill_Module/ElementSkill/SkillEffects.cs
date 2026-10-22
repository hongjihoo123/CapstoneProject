using System;
using System.Collections.Generic;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 맞은 적한테 하는 것. 스킬 에셋에서 리스트로 끼움 (예: 메테오 = [피해 40, 기절 1초])
    public enum HitEffectKind
    {
        Damage,     // value = 기본 피해 (스탯 계수는 스킬의 공격력/속성 계수 따라감)
        Burn,       // value = 초당 피해, duration = 시간
        Slow,       // value = 감소 %, duration = 시간
        Root,       // duration = 시간
        Stun,       // duration = 시간
        Knockback   // value = 미는 거리
    }

    [Serializable]
    public struct HitEffect
    {
        public HitEffectKind kind;
        public float value;
        public float duration;
    }

    // 나(시전자)한테 하는 것 (회복, 잠깐 버프)
    public enum SelfEffectKind
    {
        Heal,           // value = 회복량
        Defense,        // value = 받는 피해 감소 %, duration
        MoveSpeed,      // value = 이동 속도 +%, duration
        AttackSpeed,    // value = 공격 속도 +%, duration
        Damage          // value = 공격력 +%, duration
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

        // push = 밀치기 방향 (판정 모양이 정해줌: 원형이면 가운데에서 바깥, 직선이면 날아가는 방향)
        public static void Apply(ISkillContext context, SkillData skill, IReadOnlyList<HitEffect> effects,
            IDamageable target, Vector3 push)
        {
            ICrowdControlTarget cc = null;
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
                        context.ApplyDamageOverTime(target, effect.value * BurnTick, BurnTick, effect.duration);
                        break;
                    case HitEffectKind.Slow:
                        (cc ??= CrowdControl.Of(target))?.ApplySlow(effect.value, effect.duration);
                        break;
                    case HitEffectKind.Root:
                        (cc ??= CrowdControl.Of(target))?.ApplyRoot(effect.duration);
                        break;
                    case HitEffectKind.Stun:
                        (cc ??= CrowdControl.Of(target))?.ApplyStun(effect.duration);
                        break;
                    case HitEffectKind.Knockback:
                        (cc ??= CrowdControl.Of(target))?.ApplyKnockback(push, effect.value);
                        break;
                }
            }
        }

        // source = 이번 시전 (버프 지울 때 이걸로 찾음). 시간 있는 버프는 스탯 쪽에서 알아서 끝남
        public static void ApplySelf(ISkillContext context, IReadOnlyList<SelfEffect> effects, object source)
        {
            foreach (SelfEffect effect in effects)
            {
                switch (effect.kind)
                {
                    case SelfEffectKind.Heal:
                        context.Heal(effect.value);
                        break;
                    case SelfEffectKind.Defense:
                        AddBuff(context, PlayerStatId.Defense, StatModifierType.Flat, effect, source);
                        break;
                    case SelfEffectKind.MoveSpeed:
                        AddBuff(context, PlayerStatId.WalkSpeed, StatModifierType.PercentAdd, effect, source);
                        AddBuff(context, PlayerStatId.RunSpeed, StatModifierType.PercentAdd, effect, source);
                        break;
                    case SelfEffectKind.AttackSpeed:
                        AddBuff(context, PlayerStatId.AttackSpeed, StatModifierType.PercentAdd, effect, source);
                        break;
                    case SelfEffectKind.Damage:
                        AddBuff(context, PlayerStatId.Damage, StatModifierType.PercentAdd, effect, source);
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

        // 툴팁 줄
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
                    _ => default
                });
            }
        }
    }
}
