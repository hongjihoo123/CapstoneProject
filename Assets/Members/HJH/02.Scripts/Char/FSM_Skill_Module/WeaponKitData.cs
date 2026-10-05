using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    [CreateAssetMenu(menuName = "Character/Weapon Kit")]
    public class WeaponKitData : ScriptableObject
    {
        public WeaponData weapon;
        public SkillData dash;
        public SkillData basic;
        public SkillData ultimate;
        public PassiveData passive;

        public SkillData GetSkill(SkillSlotId slot)
        {
            switch (slot)
            {
                case SkillSlotId.Dash: return dash;
                case SkillSlotId.Basic: return basic;
                case SkillSlotId.Ultimate: return ultimate;
                default: return null;
            }
        }
    }
}
