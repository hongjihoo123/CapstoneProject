using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Player_1
{
    [CreateAssetMenu(menuName = "Skill/Dash")]
    public class DashSkillData : SkillData
    {
        [SerializeField] private float dashForce = 10f;
        [SerializeField, Tooltip("Dash toward WASD while moving (aim direction when standing still).")]
        private bool useMoveInputDirection = true;
        [SerializeField] private bool reloadOnDash = true;

        public override void Execute(ISkillContext context)
        {
            context.Dash(ResolveDirection(context), dashForce, Duration);

            if (reloadOnDash)
                context.Weapon?.InstantReload();
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 start = context.Transform.position;
            preview.Path(start, start + ResolveDirection(context) * dashForce * Duration, 1f);
        }

        private Vector3 ResolveDirection(ISkillContext context)
        {
            if (!useMoveInputDirection)
                return context.AimDirection;

            SkillAim.TryMoveDirection(context, out Vector3 direction);
            return direction;
        }
    }
}
