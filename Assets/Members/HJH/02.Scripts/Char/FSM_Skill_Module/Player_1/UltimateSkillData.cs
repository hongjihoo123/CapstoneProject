using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    [CreateAssetMenu(menuName = "Skill/Ultimate Ammo")]
    public class UltimateSkillData : SkillData
    {
        [SerializeField] private float ultimateDuration = 6f;

        public override void Execute(ISkillContext context)
        {
            if (context.Weapon is IUltimateWeapon ultimateWeapon)
                ultimateWeapon.ActivateUltimate(ultimateDuration);
        }
    }
}
