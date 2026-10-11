using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // A skill's cooldown, stored in absolute time so it keeps running while the skill is not equipped
    // (e.g. lying on the ground after a swap). default = every charge ready.
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

        // Time until the next charge comes back (approximate for multi-charge skills).
        public float Remaining => Recharging ? Mathf.Max(0f, RechargeEndTime - Time.time) : 0f;
    }
}
