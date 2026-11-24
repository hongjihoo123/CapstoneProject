using System.Text;
using UnityEngine;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using Members.KYR._01.Scripts.Enchant.So;
using Members.KYR._01.Scripts.Stats;

namespace Members.KYR._01.Scripts.Enchant.Data
{
    // 노드 효과를 사람이 읽는 문장으로 바꾼다. 에디터와 게임 UI가 같이 쓴다.
    public static class EnchantText
    {
        public static string StatLabel(PlayerStatId stat)
        {
            switch (stat)
            {
                case PlayerStatId.Damage: return "공격력";
                case PlayerStatId.MagicDamage: return "마법 공격력";
                case PlayerStatId.AttackSpeed: return "공격 속도";
                case PlayerStatId.ArmorPenetration: return "물리 관통력";
                case PlayerStatId.MagicPenetration: return "마법 관통력";
                case PlayerStatId.Omnivamp: return "피해 흡혈";
                case PlayerStatId.SkillCooldownReduction: return "스킬 쿨타임 감소";
                case PlayerStatId.FirePenetration: return "화염 관통력";
                case PlayerStatId.WaterPenetration: return "물 관통력";
                case PlayerStatId.WindPenetration: return "바람 관통력";
                case PlayerStatId.ElectricPenetration: return "전기 관통력";
                case PlayerStatId.EarthPenetration: return "대지 관통력";
                case PlayerStatId.MaxHp: return "최대 체력";
                case PlayerStatId.DamageReduction: return "피해 감소";
                case PlayerStatId.IncomingDamageTaken: return "받는 피해 배율";
                case PlayerStatId.HealReceived: return "받는 회복 배율";
                case PlayerStatId.Tenacity: return "강인함";
                case PlayerStatId.ArmorDefense: return "물리 방어력";
                case PlayerStatId.MagicDefense: return "마법 방어력";
                case PlayerStatId.MoveSpeed: return "이동 속도";
                case PlayerStatId.DashSpeed: return "대시 속도";
                case PlayerStatId.DashDuration: return "대시 지속시간";
                default: return stat.ToString();
            }
        }

        public static string Value(StatModifierType type, float value)
        {
            string sign = value >= 0f ? "+" : "-";
            float abs = value < 0f ? -value : value;
            switch (type)
            {
                case StatModifierType.Flat: return sign + abs.ToString("0.##");
                case StatModifierType.PercentAdd: return sign + (abs * 100f).ToString("0.#") + "%";
                case StatModifierType.PercentMore: return "×" + (1f + value).ToString("0.###");
                default: return value.ToString("0.##");
            }
        }

        public static string Effect(EnchantStatEffect effect, int rank)
        {
            if (effect == null)
                return string.Empty;

            return StatLabel(effect.stat) + " " + Value(effect.type, effect.ValueAt(rank));
        }

        public static string SkillEffect(EnchantSkillEffect effect, int rank)
        {
            if (effect == null)
                return string.Empty;

            float value = effect.ValueAt(rank);
            string prefix = effect.scope == SkillUpgradeScope.All ? string.Empty : SkillUpgradeScopes.Label(effect.scope) + " ";
            return prefix + SkillUpgradeScopes.Label(effect.kind) + " " + SkillUpgradeScopes.Format(effect.kind, value);
        }

        // rank < 0이면 "랭크당"으로 적는다. 스탯 효과 다음에 스킬 강화 효과.
        public static string Effects(EnchantTreeNode node, int rank, string separator = "\n")
        {
            if (node == null)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            if (node.skillEffects != null)
            {
                foreach (EnchantSkillEffect skillEffect in node.skillEffects)
                {
                    if (skillEffect == null)
                        continue;
                    if (builder.Length > 0)
                        builder.Append(separator);
                    builder.Append(rank < 0 ? SkillEffect(skillEffect, 1) + "/랭크" : SkillEffect(skillEffect, rank));
                }
            }

            if (node.effects == null || node.effects.Count == 0)
                return builder.ToString();

            for (int i = 0; i < node.effects.Count; i++)
            {
                EnchantStatEffect effect = node.effects[i];
                if (effect == null)
                    continue;

                if (builder.Length > 0)
                    builder.Append(separator);
                builder.Append(rank < 0 ? Effect(effect, 1) + "/랭크" : Effect(effect, rank));
            }

            return builder.ToString();
        }
    }
}
