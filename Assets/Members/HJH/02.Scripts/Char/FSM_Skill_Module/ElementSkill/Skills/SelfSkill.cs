using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 나한테 버프. 버프 도는 동안 몸에 이펙트 붙어 있고, explodeRadius 주면 explodeDelay 뒤에 내 자리에서 터짐
    // 반사 시간 주면 그동안 나를 때린 적한테 "맞은 적한테" 효과가 감 (전기 장막)
    // 예) 바위 방패 (3초 피해 감소 → 끝날 때 주변 폭발 + 밀치기), 폭주, 순풍, 과충전, 전기 장막
    [CreateAssetMenu(menuName = "Skill/Element/Self (버프)")]
    public class SelfSkill : ElementSkillBase
    {
        [Header("나한테")]
        [SerializeField] private List<SelfEffect> onSelf = new();
        [SerializeField, Tooltip("버프 도는 동안 몸에 붙어 있는 이펙트")] private FxSlot auraFx = new();

        [Header("끝날 때 폭발 (0 = 없음)")]
        [SerializeField] private float explodeDelay = 3f;
        [SerializeField] private float explodeRadius;

        [Header("반사 (0 = 없음): 이 시간 동안 나를 때린 적한테 '맞은 적한테' 효과")]
        [SerializeField] private float retaliateTime;
        [SerializeField, Tooltip("때린 적을 모를 때 (원거리 등) 이 범위 안 제일 가까운 적")] private float retaliateRadius = 5f;

        protected override IReadOnlyList<SelfEffect> SelfEffects => onSelf;

        protected override ISkillExecution Cast(ElementSkillCast cast)
        {
            cast.ApplySelf(onSelf);
            Fx.Shockwave(cast.Position, cast.Color, 1.2f);
            cast.Context.PlayShake(0.08f, 0.12f);

            FxHandle aura = cast.Hold(auraFx, cast.Position + Vector3.up, Vector3.forward);
            if (aura != null)
                Fx.FollowFor(aura, cast.Context.Transform, Vector3.up, Mathf.Max(LongestBuff(), explodeDelay, retaliateTime, 1f));

            if (explodeRadius > 0f)
                cast.Context.Run(new DelayedImpact(cast, null, explodeDelay, explodeRadius));
            if (retaliateTime > 0f)
                cast.Context.Run(new Retaliate(this, cast));
            return null;
        }

        private float LongestBuff()
        {
            float longest = 0f;
            foreach (SelfEffect effect in onSelf)
                longest = Mathf.Max(longest, effect.duration);
            return longest;
        }

        public override void DescribePreview(ISkillContext context, ISkillPreview preview)
        {
            if (explodeRadius > 0f)
                preview.Circle(context.Transform.position, explodeRadius);
        }

        protected override void DescribeShape(List<SkillStat> stats)
        {
            if (explodeRadius > 0f)
                stats.Add(new SkillStat("끝날 때 폭발", $"{explodeDelay:0.#}초 뒤 · {explodeRadius:0.#}m"));
            if (retaliateTime > 0f)
                stats.Add(new SkillStat("반사", $"{retaliateTime:0.#}초 동안 때린 적에게"));
        }

        // 반사 도는 동안: 맞을 때마다 때린 적한테 번개 줄 + 효과 (같은 적은 0.4초에 한 번)
        private sealed class Retaliate : ISkillTimedEffect
        {
            private const float PerTargetGap = 0.4f;

            private readonly SelfSkill _skill;
            private readonly ElementSkillCast _cast;
            private readonly Dictionary<IDamageable, float> _last = new();
            private float _left;
            private bool _subscribed;

            public Retaliate(SelfSkill skill, ElementSkillCast cast)
            {
                _skill = skill;
                _cast = cast;
                _left = skill.retaliateTime;
                cast.Context.DamageTaken += OnDamaged;
                _subscribed = true;
            }

            public bool Tick(float deltaTime)
            {
                _left -= deltaTime;
                if (_left > 0f)
                    return true;
                Unsubscribe();
                return false;
            }

            private void Unsubscribe()
            {
                if (!_subscribed)
                    return;
                _subscribed = false;
                _cast.Context.DamageTaken -= OnDamaged;
            }

            private void OnDamaged(float amount, GameObject source)
            {
                IDamageable attacker = FindAttacker(source);
                if (attacker == null || !attacker.IsAlive)
                    return;
                if (_last.TryGetValue(attacker, out float time) && Time.time - time < PerTargetGap)
                    return;
                _last[attacker] = Time.time;

                Vector3 me = _cast.Position + Vector3.up * 1.1f;
                Vector3 them = ElementSkillCast.PositionOf(attacker, me) + Vector3.up * 1.1f;
                Fx.Beam(me, them, _cast.Color, 0.25f, 0.18f);
                _cast.HitAgain(attacker, them - me);
                _cast.Feel(1);
            }

            private IDamageable FindAttacker(GameObject source)
            {
                if (source != null && source.transform != _cast.Context.Transform)
                {
                    IDamageable direct = source.GetComponentInParent<IDamageable>();
                    if (direct != null && !ReferenceEquals(direct, _cast.Context.Transform.GetComponent<IDamageable>()))
                        return direct;
                }

                IDamageable nearest = null;
                float best = float.MaxValue;
                foreach (IDamageable target in _cast.Context.OverlapSphere(_cast.Position + Vector3.up, _skill.retaliateRadius))
                {
                    float distance = (ElementSkillCast.PositionOf(target, _cast.Position) - _cast.Position).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        nearest = target;
                    }
                }
                return nearest;
            }
        }
    }
}
