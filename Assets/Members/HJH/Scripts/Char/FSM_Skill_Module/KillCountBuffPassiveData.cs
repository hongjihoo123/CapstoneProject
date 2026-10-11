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

        public override IPassive CreateRuntime(IPassiveHost host) => new Runtime(this, host);

        private sealed class Runtime : PassiveRuntime
        {
            private readonly KillCountBuffPassiveData _data;
            private int _killCount;

            public Runtime(KillCountBuffPassiveData data, IPassiveHost host) : base(host) => _data = data;

            public override void OnEnemyKilled()
            {
                _killCount++;
                if (_killCount < _data.killThreshold) return;
                _killCount = 0;

                if (!Context.StatsReady) return;

                Context.RemoveStatModifiers(this);

                foreach (BuffEntry buff in _data.buffs)
                {
                    if (!BuffSkillData.TryMap(buff.type, out PlayerStatId id))
                        continue;

                    Context.AddStatModifier(
                        id,
                        new StatModifier(this, StatModifierType.PercentAdd, buff.multiplier - 1f, buff.duration));
                }
            }

            public override void Dispose()
            {
                if (Context.StatsReady)
                    Context.RemoveStatModifiers(this);
            }
        }
    }
}
