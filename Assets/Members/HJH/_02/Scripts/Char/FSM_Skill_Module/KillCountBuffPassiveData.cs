using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Player_1;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    [CreateAssetMenu(menuName = "Skill/Passive/Kill Count Buff")]
    public class KillCountBuffPassiveData : PassiveData
    {
        [SerializeField] private int killThreshold = 5;
        [SerializeField] private BuffEntry[] buffs;

        private int _killCount;

        public override void OnEnemyKilled(ISkillContext context)
        {
            _killCount++;
            if (_killCount < killThreshold) return;
            _killCount = 0;

            if (!context.StatsReady) return;

            context.RemoveStatModifiers(this);

            foreach (BuffEntry buff in buffs)
            {
                if (!BuffSkillData.TryMap(buff.type, out PlayerStatId id))
                    continue;

                context.AddStatModifier(
                    id,
                    new StatModifier(this, StatModifierType.PercentAdd, buff.multiplier - 1f, buff.duration));
            }
        }
    }
}
