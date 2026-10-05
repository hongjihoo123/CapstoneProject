using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public abstract class SkillData : ScriptableObject
    {
        [SerializeField] private float cooldown;
        [SerializeField] private float duration;
        [SerializeField] private bool allowsMove = true;
        [SerializeField] private bool allowsFire = true;
        [SerializeField] private float moveSpeedMultiplier = 1f;

        public float Cooldown => cooldown;
        public float Duration => duration;
        public bool AllowsMove => allowsMove;
        public bool AllowsFire => allowsFire;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;

        public virtual ISkillExecution Begin(ISkillContext context)
        {
            Execute(context);
            return null;
        }

        public virtual void Execute(ISkillContext context) { }

        public virtual void OnAnimationHitEvent(ISkillContext context) { }
    }
}
