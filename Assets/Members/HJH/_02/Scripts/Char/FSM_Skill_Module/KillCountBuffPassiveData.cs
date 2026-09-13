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

        public override void OnEnemyKilled(SkillStateModule owner)
        {
            _killCount++;
            Debug.Log($"[패시브] 킬 카운트 = {_killCount}");
            if (_killCount < killThreshold) return;
            _killCount = 0;

            var stats = owner.Player.Stats;
            Debug.Log($"[패시브] 발동! stats null? {stats == null || stats.Tree == null}");
            if (stats == null || stats.Tree == null) return;

            stats.RemoveModifiers(this);

            foreach (var buff in buffs)
            {
                if (!BuffSkillData.TryMap(buff.type, out PlayerStatId id))
                    continue;

                stats.AddModifier(
                    id,
                    new StatModifier(this, StatModifierType.PercentAdd, buff.multiplier - 1f, buff.duration));
            }
        }
    }
}
