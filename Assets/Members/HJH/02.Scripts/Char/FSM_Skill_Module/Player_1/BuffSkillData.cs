using System;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Player_1
{
    [Serializable]
    public struct BuffEntry
    {
        public BuffType type;
        public float multiplier;
        public float duration;
    }

    [CreateAssetMenu(menuName = "Skill/Buff")]
    public class BuffSkillData : SkillData
    {
        [SerializeField] private BuffEntry[] buffs;

        public override void Execute(ISkillContext context)
        {
            if (!context.StatsReady) return;

            context.RemoveStatModifiers(this);

            foreach (BuffEntry buff in buffs)
            {
                if (!TryMap(buff.type, out PlayerStatId id))
                    continue;

                context.AddStatModifier(
                    id,
                    new StatModifier(this, StatModifierType.PercentAdd, buff.multiplier - 1f, buff.duration));
            }
        }

        public static bool TryMap(BuffType type, out PlayerStatId id)
        {
            switch (type)
            {
                case BuffType.AttackSpeed:
                    id = PlayerStatId.AttackSpeed;
                    return true;
                case BuffType.Damage:
                    id = PlayerStatId.Damage;
                    return true;
                case BuffType.ReloadSpeed:
                    id = PlayerStatId.ReloadSpeed;
                    return true;
                case BuffType.MoveSpeed:
                    id = PlayerStatId.Mobility;
                    return true;
                default:
                    id = default;
                    return false;
            }
        }
    }
}
