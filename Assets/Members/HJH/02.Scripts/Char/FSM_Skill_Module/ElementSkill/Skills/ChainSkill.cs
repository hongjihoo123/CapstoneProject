using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 커서에 제일 가까운 적부터 맞고, 거기서 가까운 적으로 차례차례 튐 (같은 적은 한 번)
    // 예) 연쇄 번개
    [CreateAssetMenu(menuName = "Skill/Element/Chain (튀기)")]
    public class ChainSkill : ElementSkillBase
    {
        [Header("튀기")]
        [SerializeField, Tooltip("첫 적을 찾는 거리 (나 기준)")] private float range = 8f;
        [SerializeField, Tooltip("첫 적은 커서에서 이 거리 안에서 찾음")] private float aimAssist = 3f;
        [SerializeField, Tooltip("다음 적으로 튈 수 있는 거리")] private float bounceRange = 4.5f;
        [SerializeField, Min(1), Tooltip("맞는 적 최대 수 (첫 적 포함)")] private int maxTargets = 5;
        [SerializeField, Tooltip("한 번 튀는 데 걸리는 시간")] private float jumpDelay = 0.08f;
        [SerializeField, Tooltip("줄 굵기")] private float beamWidth = 0.35f;

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            Vector3 aim = cast.Aim(range, out float distance);
            Vector3 cursor = cast.Position + aim * distance;
            cast.PlayCast(aim);
            cast.Recoil(aim, 0.1f);

            IDamageable first = Nearest(cast, cursor, aimAssist, null);
            if (first == null)
            {
                // 아무도 없으면 커서까지 헛방
                Vector3 from = cast.Position + Vector3.up * 1.1f;
                Fx.Beam(from, cursor + Vector3.up * 0.3f, cast.Color, beamWidth * 0.7f, 0.15f);
                cast.PlayImpact(cast.Ground(cursor), 0.6f);
                cast.Feel(0);
                return null;
            }

            cast.Context.Run(new Bounce(this, cast, first));
            return null;
        }

        private static IDamageable Nearest(ElementSkillCast cast, Vector3 point, float radius, HashSet<IDamageable> skip)
        {
            IDamageable best = null;
            float bestDistance = float.MaxValue;
            foreach (IDamageable target in cast.Context.OverlapSphere(point + Vector3.up * 0.5f, radius))
            {
                if (!target.IsAlive || (skip != null && skip.Contains(target)))
                    continue;
                float d = (ElementSkillCast.PositionOf(target, point) - point).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = target;
                }
            }
            return best;
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 position = context.Transform.position;
            preview.RangeCircle(position, range);
            Vector3 direction = SkillAim.Resolve(context, 0f, range, out float distance);
            preview.Circle(position + direction * distance, aimAssist);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            stats.Add(new SkillStat("사거리", $"{range:0.#}m"));
            stats.Add(new SkillStat("튀는 수", $"최대 {maxTargets}명 · {bounceRange:0.#}m"));
        }

        // 한 칸씩 튐
        private sealed class Bounce : ISkillTimedEffect
        {
            private readonly ChainSkill _skill;
            private readonly ElementSkillCast _cast;
            private readonly HashSet<IDamageable> _done = new();
            private IDamageable _next;
            private Vector3 _from;
            private float _wait;
            private int _count;

            public Bounce(ChainSkill skill, ElementSkillCast cast, IDamageable first)
            {
                _skill = skill;
                _cast = cast;
                _next = first;
                _from = cast.Position + Vector3.up * 1.1f;
            }

            public bool Tick(float deltaTime)
            {
                _wait -= deltaTime;
                if (_wait > 0f)
                    return true;

                if (_next == null || !_next.IsAlive || _count >= _skill.maxTargets)
                    return false;

                Vector3 to = ElementSkillCast.PositionOf(_next, _from) + Vector3.up * 1.1f;
                Fx.Beam(_from, to, _cast.Color, _skill.beamWidth, 0.2f);
                Fx.Hit(to, to - _from, _cast.Color, true);
                _cast.Hit(_next, to - _from);
                _cast.Feel(1);
                _done.Add(_next);
                _count++;

                _from = to;
                _next = Nearest(_cast, to - Vector3.up * 1.1f, _skill.bounceRange, _done);
                _wait = _skill.jumpDelay;
                return _next != null && _count < _skill.maxTargets;
            }
        }
    }
}
