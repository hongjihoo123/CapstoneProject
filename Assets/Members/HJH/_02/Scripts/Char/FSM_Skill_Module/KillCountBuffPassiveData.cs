using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Player_1;
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

            var status = owner.Player.GetModule<StatusEffectModule>();
            Debug.Log($"[패시브] 발동! status null? {status == null}");
            if (status == null) return;

            foreach (var buff in buffs)
                status.Apply(buff.type, buff.multiplier, buff.duration);
        }
    }
}