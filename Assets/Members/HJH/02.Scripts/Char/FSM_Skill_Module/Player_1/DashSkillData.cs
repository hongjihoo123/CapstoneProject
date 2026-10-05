using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Player_1
{
    [CreateAssetMenu(menuName = "Skill/Dash")]
    public class DashSkillData : SkillData
    {
        [SerializeField] private float dashForce = 10f;
        [SerializeField] private bool useMoveInputDirection;
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
            Vector3 input = context.MoveInputDirection;
            return useMoveInputDirection && input.sqrMagnitude > 0.0001f ? input : context.AimDirection;
        }
    }
}
