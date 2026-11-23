using System;
using Members.KYR._01_Scripts.EffectBuffs;
using Members.KYR._01.Scripts.Stats;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Element
{
    // 콤보가 터졌을 때 걸리는 임시 효과 하나 (KYR 의 EffectBuff 를 그대로 이어받음).
    //  - 지속 시간 동안 스탯 올리기 (여러 개 가능)
    //  - 터지는 순간 한 번: 체력 회복(최대 체력의 몇 %) / 쉴드
    // 같은 콤보를 다시 터뜨리면 시간만 처음부터 다시 (EffectBuffRunner 규칙) -> 회복·쉴드는 효과가 끝난 뒤에 다시 터뜨려야 또 들어옴
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

        // 스탯을 걸 때 "누가 걸었는지" 표시로 쓰는 것. 끝날 때 이걸로 내가 건 것만 걷어냄
        public sealed class State { }

        // 스탯 modifier 의 출처가 콤보 효과인가 (HUD 가 "콤보가 준 몫"을 따로 보여줄 때 씀)
        public static bool IsComboSource(object source) => source is State;

        public void Configure(Mod[] mods, float heal, float shieldAmount)
        {
            modifiers = mods ?? Array.Empty<Mod>();
            healPercent = heal;
            shield = shieldAmount;
        }

        // 게임 중에 만든 효과(바인딩 콤보)의 이름과 시간. EffectBuff 쪽 필드가 private 이라 직렬화 값으로 덮어씀
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
