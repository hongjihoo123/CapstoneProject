using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 커서 쪽으로 돌진 or 순간이동
    //  - pathRadius 주면 지나가는 길에 있는 적 때림 (돌진만)
    //  - landRadius 주면 도착한 자리에서 터짐 (순간이동은 출발한 자리도)
    // 예) 화염 돌진, 도약, 섬광
    // 돌진은 여러 프레임 걸려서 스킬 Duration 을 dashTime 보다 조금 길게 줘야 함
    [CreateAssetMenu(menuName = "Skill/Element/Dash (돌진)")]
    public class DashSkill : ElementSkillBase
    {
        [Header("돌진")]
        [SerializeField] private float distance = 6f;
        [SerializeField, Tooltip("켜면 날아가지 않고 바로 순간이동")] private bool blink;
        [SerializeField, Tooltip("돌진에 걸리는 시간")] private float dashTime = 0.2f;
        [SerializeField, Tooltip("0 = 지나가는 길은 안 때림")] private float pathRadius;
        [SerializeField, Tooltip("0 = 도착해도 안 터짐")] private float landRadius;

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

        // 벽에 박히지 않게, 몸이 들어가는 제일 먼 자리까지만
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

        // 돌진하는 동안 매 프레임
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
            }

            public void Tick(float elapsed)
            {
                if (_landed)
                    return;

                // 지나가는 길: 플레이어 따라다니는 원 안에 들어온 적
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

            // 스킬이 끝나거나 끊겨도 착지는 꼭 함
            public void End() => Land();

            private void Land()
            {
                if (_landed)
                    return;
                _landed = true;

                if (_skill.landRadius > 0f)
                    _cast.Impact(_cast.Position, _skill.landRadius);
            }
        }
    }
}
