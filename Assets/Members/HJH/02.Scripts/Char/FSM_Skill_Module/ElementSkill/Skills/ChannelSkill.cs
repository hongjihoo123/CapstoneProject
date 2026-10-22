using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 누르고 있는 동안(= 스킬 Duration 동안) 커서 쪽으로 부채꼴을 계속 뿜음. tick 마다 안에 있는 적 다시 맞음
    // 예) 화염 방사
    // 스킬 Duration = 뿜는 시간. 걸으면서 쏘려면 allowsMove + moveSpeedMultiplier 로 느리게
    [CreateAssetMenu(menuName = "Skill/Element/Channel (계속 뿜기)")]
    public class ChannelSkill : ElementSkillBase
    {
        [Header("계속 뿜기")]
        [SerializeField] private float length = 5f;
        [SerializeField, Tooltip("전체 각도")] private float angle = 40f;
        [SerializeField, Tooltip("몇 초마다 판정 + 이펙트 한 번")] private float tickInterval = 0.15f;
        [SerializeField, Tooltip("tick 마다 입에서 앞으로 뿜는 이펙트 (불길 한 줄기)")] private FxSlot streamFx = new();

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            cast.PlayCast(cast.Context.AimDirection);
            return new Stream(this, cast);
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            Vector3 direction = SkillAim.Resolve(context, 0f, length, out _);
            preview.Cone(context.Transform.position, direction, length, angle);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            stats.Add(new SkillStat("길이", $"{length:0.#}m"));
            stats.Add(new SkillStat("각도", $"{angle:0}°"));
            stats.Add(new SkillStat("뿜는 시간", $"{Duration:0.#}초 ({tickInterval:0.##}초마다)"));
        }

        private sealed class Stream : ISkillExecution
        {
            private readonly ChannelSkill _skill;
            private readonly ElementSkillCast _cast;
            private float _next;

            public Stream(ChannelSkill skill, ElementSkillCast cast)
            {
                _skill = skill;
                _cast = cast;
            }

            public void Tick(float elapsed)
            {
                if (elapsed < _next)
                    return;
                _next += Mathf.Max(0.05f, _skill.tickInterval);
                Puff();
            }

            private void Puff()
            {
                Vector3 apex = _cast.Position;
                Vector3 direction = _cast.Context.AimDirection;
                Quaternion facing = Quaternion.LookRotation(direction);

                AttackAreaBus.Raise(AttackArea.Cone(AttackAreaKind.Skill, apex, facing, _skill.length, _skill.angle, _skill.tickInterval));
                _cast.Play(_skill.streamFx, apex + Vector3.up * 1.1f + direction * 0.5f, direction);
                _cast.Context.PlayShake(0.03f, _skill.tickInterval);

                int hits = 0;
                foreach (IDamageable target in _cast.Context.OverlapSphere(apex + Vector3.up, _skill.length))
                {
                    Vector3 toTarget = ElementSkillCast.PositionOf(target, apex) - apex;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(direction, toTarget) > _skill.angle * 0.5f)
                        continue;
                    _cast.HitAgain(target, direction);
                    hits++;
                }

                if (hits > 0)
                    _cast.Context.PlayHitFeel(0f, _cast.Skill.Shake * 0.5f);
            }

            public void OnAnimationEvent(SkillAnimationEvent animationEvent) { }

            public void End() { }
        }
    }
}
