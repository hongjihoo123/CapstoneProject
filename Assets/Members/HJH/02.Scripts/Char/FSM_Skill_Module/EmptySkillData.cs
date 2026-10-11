using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Placeholder skill: does nothing on cast, but still goes through cooldown and SkillUsed
    // (so it adds its element stack). Swap the asset for a real skill later.
    [CreateAssetMenu(menuName = "Skill/Empty (Placeholder)")]
    public class EmptySkillData : SkillData
    {
    }
}
