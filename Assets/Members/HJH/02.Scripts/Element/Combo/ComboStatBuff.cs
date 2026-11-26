using System;
using Members.KYR._01_Scripts.EffectBuffs;
using Members.KYR._01.Scripts.Stats;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    [CreateAssetMenu(menuName = "Combo/Stat Buff")]
    public class ComboStatBuff : EffectBuff<ComboStatBuff.State>
    {
        [Serializable]
        public struct Mod
        {
            public PlayerStatId stat;
            public StatModifierType type;
            [Tooltip("PercentAdd: 0.15 = +15%")] public float value;

            public Mod(PlayerStatId stat, StatModifierType type, float value)
            {
                this.stat = stat;
                this.type = type;
                this.value = value;
            }
        }

        [SerializeField] private Mod[] modifiers = Array.Empty<Mod>();
        [SerializeField, Range(0f, 1f), Tooltip("터지는 순간 최대 체력의 이만큼 회복 (0.08 = 8%)")] private float healPercent;
        [SerializeField, Tooltip("터지는 순간 쉴드")] private float shield;

        public sealed class State { }

        public static bool IsComboSource(object source) => source is State;

        public void Configure(Mod[] mods, float heal, float shieldAmount)
        {
            modifiers = mods ?? Array.Empty<Mod>();
            healPercent = heal;
            shield = shieldAmount;
        }

        public void SetInfo(string displayName, float duration)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Info { displayName = displayName, duration = duration }), this);
        }

        [Serializable]
        private struct Info
        {
            public string displayName;
            public float duration;
        }

        protected override void OnStart(EffectBuffContext context, State state)
        {
            var player = context.Player;
            if (player == null)
                return;

            if (player.Stats != null && player.Stats.Tree != null)
            {
                foreach (Mod mod in modifiers)
                    player.Stats.AddModifier(mod.stat, new StatModifier(state, mod.type, mod.value));

                if (healPercent > 0f)
                    player.Heal(player.Stats.Get(PlayerStatId.MaxHp) * healPercent);
            }

            if (shield > 0f)
                player.AddShield(shield);
        }

        protected override void OnEnd(EffectBuffContext context, State state)
        {
            var player = context.Player;
            if (player != null && player.Stats != null && player.Stats.Tree != null)
                player.Stats.RemoveModifiers(state);
        }
    }
}
