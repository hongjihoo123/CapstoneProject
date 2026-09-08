using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    [CreateAssetMenu(menuName = "Skill/Ultimate Ammo")]
    public class UltimateSkillData : SkillData
    {
        [SerializeField] private float ultimateDuration = 6f;

        public override void Execute(SkillStateModule owner)
        {
            if (owner.Player.Weapon.Weapon is RobotWeapons.GunDealerWeapon gunDealer)
                gunDealer.ActivateUltimate(ultimateDuration);
        }
    }
}