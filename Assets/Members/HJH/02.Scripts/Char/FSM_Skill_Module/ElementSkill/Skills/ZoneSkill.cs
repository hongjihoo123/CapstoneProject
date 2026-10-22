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
        [SerializeField, Tooltip("장판 유지 시간")] private float zoneTime = 4f;
        [SerializeField, Tooltip("몇 초마다 판정")] private float interval = 1f;
        [SerializeField, Tooltip("켜면 장판이 나를 따라다님 (불의 장막)")] private bool followSelf;
        [SerializeField, Tooltip("판정마다 화면 흔들림 (지진). 0 = 없음")] private float pulseShake;
        [SerializeField, Tooltip("0 = 안에 있는 적 전부. 1 이상 = 판정마다 안에서 무작위 N명만 (번개가 떨어짐, 폭풍)")] private int strikesPerPulse;
        [SerializeField, Tooltip("무작위로 맞을 때 그 자리 터지는 범위")] private float strikeRadius = 1.2f;
        [SerializeField, Tooltip("장판 깔려 있는 동안 계속 붙어 있는 이펙트 (바닥 불길, 소용돌이 등)")] private FxSlot zoneFx = new();

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
            stats.Add(new SkillStat("유지", $"{zoneTime:0.#}초 ({interval:0.##}초마다)"));
        }

        // 깔려 있는 동안 매 프레임 도는 거
        private sealed class Zone : ISkillTimedEffect
        {
            private readonly ZoneSkill _skill;
            private readonly ElementSkillCast _cast;
            private readonly FxHandle _circle;
            private readonly FxHandle _body;
            private Vector3 _center;
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
                _body = cast.Hold(skill.zoneFx, _center, cast.Context.AimDirection, skill.radius / ElementSkillCast.AuthoredRadius);
            }

            public bool Tick(float deltaTime)
            {
                _elapsed += deltaTime;
                if (_skill.followSelf)
                {
                    _center = _cast.Ground(_cast.Position);
                    _circle.MoveTo(_center);
                    _body?.MoveTo(_center);
                }
                AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, _center, _skill.radius, 0.02f));

                if (_elapsed >= _nextPulse)
                {
                    _nextPulse += Mathf.Max(0.05f, _skill.interval);
                    Pulse();
                }

                if (_elapsed < _skill.zoneTime)
                    return true;

                _circle.Kill();
                _body?.Kill();
                return false;
            }

            private void Pulse()
            {
                if (_skill.strikesPerPulse <= 0)   // 무작위 낙뢰는 맞는 자리에서 터짐
                    _cast.PlayImpact(_center, _skill.radius * 0.8f);
                if (_skill.pulseShake > 0f)
                    _cast.Context.PlayShake(_skill.pulseShake, Mathf.Min(0.3f, _skill.interval));

                IReadOnlyList<IDamageable> inside = _cast.Context.OverlapSphere(_center + Vector3.up * 0.5f, _skill.radius);
                if (_skill.strikesPerPulse > 0)
                    Strike(inside);
                else
                    foreach (IDamageable target in inside)
                        _cast.HitAgain(target, ElementSkillCast.PositionOf(target, _center) - _center);

                Vector3 me = _cast.Position - _center;
                me.y = 0f;
                if (me.sqrMagnitude <= _skill.radius * _skill.radius)
                    _cast.ApplySelf(_skill.onSelf, _skill.selfFx);
            }

            // 안에 있는 적 중 무작위 N명 머리 위로 번개 (아무도 없으면 빈 자리에 한 번)
            private void Strike(IReadOnlyList<IDamageable> inside)
            {
                var pool = new List<IDamageable>(inside);
                if (pool.Count == 0)
                {
                    Vector2 random = Random.insideUnitCircle * _skill.radius * 0.8f;
                    Vector3 empty = _cast.Ground(_center + new Vector3(random.x, 0f, random.y));
                    Fx.Beam(empty + Vector3.up * 9f, empty, _cast.Color, 0.35f, 0.2f);
                    _cast.Play(_cast.Skill.ImpactFx, empty, Vector3.forward, _skill.strikeRadius / ElementSkillCast.AuthoredRadius);
                    return;
                }

                for (int i = 0; i < _skill.strikesPerPulse && pool.Count > 0; i++)
                {
                    int pick = Random.Range(0, pool.Count);
                    IDamageable target = pool[pick];
                    pool.RemoveAt(pick);
                    Vector3 at = _cast.Ground(ElementSkillCast.PositionOf(target, _center));
                    Fx.Beam(at + Vector3.up * 9f, at + Vector3.up, _cast.Color, 0.4f, 0.22f);
                    _cast.Play(_cast.Skill.ImpactFx, at, Vector3.forward, _skill.strikeRadius / ElementSkillCast.AuthoredRadius);
                    foreach (IDamageable near in _cast.Context.OverlapSphere(at + Vector3.up * 0.5f, _skill.strikeRadius))
                        _cast.HitAgain(near, ElementSkillCast.PositionOf(near, at) - at);
                    _cast.Feel(1, groundSlam: true, size: _skill.strikeRadius);
                }
            }
        }
    }
}
