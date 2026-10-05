using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Player_1
{
    [CreateAssetMenu(menuName = "Skill/Dash")]
    public class DashSkillData : SkillData
    {
        [SerializeField] private float dashForce = 10f;
        [SerializeField] private bool useMoveInputDirection = true;
        [SerializeField] private bool reloadOnDash = true;

        public override void Execute(ISkillContext context)
        {
            Vector3 direction = useMoveInputDirection && context.MoveInputDirection.sqrMagnitude > 0.0001f
                ? context.MoveInputDirection
                : context.Transform.forward;

            context.Dash(direction, dashForce, Duration);

            if (reloadOnDash)
                context.Weapon?.InstantReload();
        }
    }
}
