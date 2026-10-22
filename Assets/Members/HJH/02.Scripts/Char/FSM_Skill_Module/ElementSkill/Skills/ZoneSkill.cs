using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 장판. 몇 초 동안 남아서 interval 마다 안에 있는 적한테 효과 (맞은 적한테 할 것 = 한 번 판정마다)
    // 내가 안에 서 있으면 나한테 효과도 (회복 장판 같은 거)
    // 예) 치유의 비
    [CreateAssetMenu(menuName = "Skill/Element/Zone (장판)")]
    public class ZoneSkill : ElementSkillBase
    {
        [Header("장판")]
        [SerializeField, Tooltip("켜면 커서 말고 내 자리에 깔림")] private bool aroundSelf;
        [SerializeField, Tooltip("커서 최대 거리")] private float range = 6f;
        [SerializeField] private float radius = 3f;
        [SerializeField, Tooltip("장판 유지 시간")] private float duration = 4f;
        [SerializeField, Tooltip("몇 초마다 판정")] private float interval = 1f;

        [Header("내가 안에 있으면 (판정마다)")]
        [SerializeField] private List<SelfEffect> onSelf = new();
        [SerializeField, Tooltip("나한테 효과 들어갈 때 내 몸에서")] private FxSlot selfFx = new();

        protected override IReadOnlyList<SelfEffect> SelfEffects => onSelf;

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            Vector3 center = cast.Target(range, aroundSelf);
            cast.PlayCast(center - cast.Position);
            cast.Context.Run(new Zone(this, cast, center));
            return null;
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 position = context.Transform.position;
            if (!aroundSelf)
                preview.RangeCircle(position, range);
            Vector3 direction = SkillAim.Resolve(context, 0f, range, out float distance);
            preview.Circle(aroundSelf ? position : position + direction * distance, radius);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            stats.Add(new SkillStat("범위", $"{radius:0.#}m"));
            stats.Add(new SkillStat("사거리", aroundSelf ? "자신 주변" : $"{range:0.#}m"));
            stats.Add(new SkillStat("유지", $"{duration:0.#}초 ({interval:0.##}초마다)"));
        }

        // 깔려 있는 동안 매 프레임 도는 거
        private sealed class Zone : ISkillTimedEffect
        {
            private readonly ZoneSkill _skill;
            private readonly ElementSkillCast _cast;
            private readonly Vector3 _center;
            private readonly FxHandle _circle;
            private float _elapsed;
            private float _nextPulse;

            public Zone(ZoneSkill skill, ElementSkillCast cast, Vector3 center)
            {
                _skill = skill;
                _cast = cast;
                _center = cast.Ground(center);
                _circle = Fx.MagicCircle(_center, skill.radius, cast.Color);
                _circle.SetValue(1f);
                _circle.SetSpeed(60f);
            }

            public bool Tick(float deltaTime)
            {
                _elapsed += deltaTime;
                AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, _center, _skill.radius, 0.02f));

                if (_elapsed >= _nextPulse)
                {
                    _nextPulse += Mathf.Max(0.05f, _skill.interval);
                    Pulse();
                }

                if (_elapsed < _skill.duration)
                    return true;

                _circle.Kill();
                return false;
            }

            private void Pulse()
            {
                _cast.PlayImpact(_center, _skill.radius * 0.8f);

                foreach (IDamageable target in _cast.Context.OverlapSphere(_center + Vector3.up * 0.5f, _skill.radius))
                    _cast.HitAgain(target, ElementSkillCast.PositionOf(target, _center) - _center);

                Vector3 me = _cast.Position - _center;
                me.y = 0f;
                if (me.sqrMagnitude <= _skill.radius * _skill.radius)
                    _cast.ApplySelf(_skill.onSelf, _skill.selfFx);
            }
        }
    }
}
