using Assets.Members.HJH._02.Scripts.Element.Upgrade;
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
        [SerializeField, Tooltip("건슬링어 연출: 출발 파동 + 조명 + 잔상")] private bool flashyFx;

        // 스킬 트리: 대쉬 거리
        private float Force => dashForce * SkillUpgrades.Multiplier(SkillUpgradeKind.Range, this);

        public override void Execute(ISkillContext context)
        {
            Vector3 direction = ResolveDirection(context);
            context.Dash(direction, Force, Duration);
            if (flashyFx)
                global::Assets.Members.HJH._02.Scripts.Char.Visual.GunnerFx.DashStart(context.Transform, direction, FxColor, Duration);

            if (reloadOnDash)
                context.Weapon?.InstantReload();
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 start = context.Transform.position;
            preview.Path(start, start + ResolveDirection(context) * Force * Duration, 1f);
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
