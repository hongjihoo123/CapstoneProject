using RobotWeapons;
using UnityEngine.Serialization;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    [CreateAssetMenu(menuName = "Character/Weapon Kit")]
    public class WeaponKitData : ScriptableObject
    {
        public WeaponData weapon;
        public SkillData dash;
        [FormerlySerializedAs("basic")] public SkillData weaponSkill;
        public SkillData ultimate;
        public PassiveData passive;

        public SkillData GetSkill(SkillSlotId slot)
        {
            switch (slot)
            {
                case SkillSlotId.Dash: return dash;
                case SkillSlotId.Weapon: return weaponSkill;
                case SkillSlotId.Ultimate: return ultimate;
                default: return null;
            }
        }
    }
}
