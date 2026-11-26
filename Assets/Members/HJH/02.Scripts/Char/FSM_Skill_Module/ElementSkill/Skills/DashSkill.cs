using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    [CreateAssetMenu(menuName = "Skill/Element/Dash (돌진)")]
    public class DashSkill : ElementSkillBase
    {
        [Header("돌진")]
        [SerializeField] private float distance = 6f;
        [SerializeField, Tooltip("켜면 날아가지 않고 바로 순간이동")] private bool blink;
        [SerializeField, Tooltip("돌진에 걸리는 시간")] private float dashTime = 0.2f;
        [SerializeField, Tooltip("0 = 지나가는 길은 안 때림")] private float pathRadius;
        [SerializeField, Tooltip("0 = 도착해도 안 터짐")] private float landRadius;
        [SerializeField, Tooltip("돌진하는 동안 몸에 붙어 따라오는 이펙트 (앞에서 공기가 갈라지는 막 등). 앞 = +Z")] private FxSlot dashFx = new();

        protected override CastMotion DefaultMotion => CastMotion.None;

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            Vector3 direction = cast.Aim(distance, out float reach);
            cast.PlayCast(direction);
            cast.Context.PlayShake(0.08f, 0.1f);

            if (!blink)
                return new DashRun(this, cast, direction, reach);

            Blink(cast, direction, reach);
            return null;
        }

        private void Blink(ElementSkillCast cast, Vector3 direction, float reach)
        {
            Vector3 origin = cast.Position;
            Vector3 destination = cast.Ground(FarthestFree(cast, origin, direction, reach) + Vector3.up);

            Fx.BlinkOut(origin, cast.Color);
            cast.Context.SetScriptedPosition(destination);
            cast.Context.CancelDash();
            Fx.BlinkIn(destination, direction, cast.Color);
            Fx.TeleportStreak(origin + Vector3.up * 0.9f, destination + Vector3.up * 0.9f, cast.Color);

            if (landRadius > 0f)
            {
                cast.Impact(origin, landRadius);
                cast.Impact(destination, landRadius);
            }
        }

        private static Vector3 FarthestFree(ElementSkillCast cast, Vector3 origin, Vector3 direction, float reach)
        {
            const int steps = 8;
            for (int i = steps; i > 0; i--)
            {
                Vector3 candidate = origin + direction * (reach * i / steps);
                if (!cast.Context.SweepEnvironment(origin, candidate))
                    return candidate;
            }
            return origin;
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 start = context.Transform.position;
            Vector3 direction = SkillAim.Resolve(context, 0f, distance, out float reach);
            Vector3 end = start + direction * reach;
            preview.RangeCircle(start, distance);
            preview.Path(start, end, Mathf.Max(0.6f, pathRadius * 2f));
            if (landRadius > 0f)
                preview.Circle(end, landRadius);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            stats.Add(new SkillStat(blink ? "순간이동 거리" : "돌진 거리", $"{distance:0.#}m"));
            if (landRadius > 0f)
                stats.Add(new SkillStat("폭발 범위", $"{landRadius:0.#}m"));
        }

        private sealed class DashRun : ISkillExecution
        {
            private readonly DashSkill _skill;
            private readonly ElementSkillCast _cast;
            private readonly Vector3 _direction;
            private bool _landed;

            public DashRun(DashSkill skill, ElementSkillCast cast, Vector3 direction, float reach)
            {
                _skill = skill;
                _cast = cast;
                _direction = direction;
                float time = Mathf.Max(0.05f, skill.dashTime);
                cast.Context.Dash(direction, Mathf.Max(0.1f, reach) / time, time);

                DashTint.Set(cast.Color, time + 0.3f);
                _fx = cast.Hold(skill.dashFx, cast.Position, direction);
                if (_fx != null)
                    Fx.FollowFor(_fx, cast.Context.Transform, Vector3.zero, time + 0.1f);
            }

            private readonly FxHandle _fx;

            public void Tick(float elapsed)
            {
                if (_landed)
                    return;

                if (_skill.pathRadius > 0f)
                {
                    foreach (IDamageable target in _cast.Context.OverlapSphere(_cast.Position + Vector3.up, _skill.pathRadius))
                    {
                        if (_cast.Hit(target, _direction))
                            _cast.Feel(1);
                    }
                }

                if (!_cast.Context.IsDashing)
                    Land();
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            public void End() => Land();

            private void Land()
            {
                if (_landed)
                    return;
                _landed = true;
                _fx?.Kill();

                if (_skill.landRadius > 0f)
                    _cast.Impact(_cast.Position, _skill.landRadius);
            }
        }
    }
}
