using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public readonly struct SkillCooldownState
    {
        public readonly int SpentCharges;
        public readonly bool Recharging;
        public readonly float RechargeEndTime;

        public SkillCooldownState(int spentCharges, bool recharging, float rechargeEndTime)
        {
            SpentCharges = spentCharges;
            Recharging = recharging;
            RechargeEndTime = rechargeEndTime;
        }

        public float Remaining => Recharging ? Mathf.Max(0f, RechargeEndTime - Time.time) : 0f;
    }
}
