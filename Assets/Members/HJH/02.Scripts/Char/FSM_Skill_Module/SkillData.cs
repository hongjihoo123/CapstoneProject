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

        // 스킬 진입 시 실제로 뭘 할지 구현 싹싹
        public abstract void Execute(SkillStateModule owner);

        // 애니메이션 이벤트 타이밍
        public virtual void OnAnimationHitEvent(SkillStateModule owner) { }
    }
}