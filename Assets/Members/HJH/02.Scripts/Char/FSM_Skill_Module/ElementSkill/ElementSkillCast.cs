using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    // 스킬 한 번 쓸 때마다 새로 만들어지는 거. 모양(자식 스킬)들은 이걸로만 때리고 터뜨림
    //  - 한 번 쓸 때 같은 적은 한 번만 맞음 (장판처럼 계속 때리는 건 Hit 대신 HitAgain)
    //  - 손맛(멈칫/흔들림/카메라 툭)이랑 이펙트(맞은 자리, CC 머리 위)도 여기서 한 번에
    public sealed class ElementSkillCast
    {
        private static readonly Vector3 BodyHeight = Vector3.up * 1.1f;
        private static readonly Vector3 HeadHeight = Vector3.up * 2.1f;

        private readonly HashSet<IDamageable> _hit = new();   // 이번에 이미 맞은 적
        private readonly float _statusTime;                   // CC 표시 띄워둘 시간 (스킬의 제일 긴 CC)

        public ElementSkillCast(ElementSkillBase skill, ISkillContext context)
        {
            Skill = skill;
            Context = context;
            Color = skill.FxColor;
            _statusTime = LongestCrowdControl(skill.OnHit);
        }

        public ElementSkillBase Skill { get; }
        public ISkillContext Context { get; }
        public Color Color { get; }
        public Vector3 Position => Context.Transform.position;

        // ------------------------------------------------ 때리기

        // 이번 시전에서 처음 맞는 적이면 효과 적용. 맞았으면 true
        public bool Hit(IDamageable target, Vector3 push)
        {
            if (!_hit.Add(target))
                return false;
            HitAgain(target, push);
            return true;
        }

        // 이미 맞은 적이어도 또 때림 (장판용). push = 밀치기 방향
        public void HitAgain(IDamageable target, Vector3 push)
        {
            SkillEffects.Apply(Context, Skill, Skill.OnHit, target, push);
            if (target is not Component component)
                return;

            Transform body = component.transform;
            Play(Skill.HitFx, body.position + BodyHeight, push);

            // CC 걸린 동안 머리 위에 표시
            if (_statusTime > 0f && Skill.StatusFx.IsSet && target.IsAlive)
            {
                FxHandle status = Hold(Skill.StatusFx, body.position + HeadHeight, Vector3.forward);
                Fx.FollowFor(status, body, HeadHeight, _statusTime);
            }
        }

        // 나한테 효과 (회복, 버프). source = 이번 시전 (스탯 쪽에서 버프 찾을 때 씀)
        public void ApplySelf(IReadOnlyList<SelfEffect> effects, FxSlot fx = null)
        {
            if (effects.Count == 0)
                return;
            SkillEffects.ApplySelf(Context, effects, this);
            if (fx != null)
                Play(fx, Position + BodyHeight, Context.AimDirection);
        }

        // 원형으로 터뜨림: 이펙트 + 판정 + 손맛. 맞은 수 돌려줌
        public int Impact(Vector3 center, float radius)
        {
            AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, center, radius, 0.2f));
            PlayImpact(Ground(center), radius);

            int hits = 0;
            foreach (IDamageable target in Context.OverlapSphere(center + Vector3.up * 0.5f, radius))
            {
                if (Hit(target, PositionOf(target, center) - center))
                    hits++;
            }

            Feel(hits, groundSlam: true, size: radius);
            return hits;
        }

        // ------------------------------------------------ 손맛

        // 많이 맞힐수록 조금 더 길게 멈칫 + 세게 흔들림 (5명까지). 바닥 찍는 건 카메라가 아래로 툭
        public void Feel(int hits, bool groundSlam = false, float size = 1f)
        {
            if (hits <= 0)
            {
                Context.PlayHitFeel(0f, Skill.Shake * 0.4f);   // 허공이어도 살짝은 흔들림
                return;
            }

            float more = 1f + 0.2f * (Mathf.Min(hits, 5) - 1);
            Context.PlayHitFeel(Skill.HitStop * more, Skill.Shake * more);
            if (groundSlam)
                Context.PlayCameraKick(Vector3.down * Mathf.Clamp(0.08f + size * 0.04f, 0.1f, 0.3f), 0.25f);
        }

        // 쏠 때 반동: 카메라가 쏘는 반대쪽으로 살짝
        public void Recoil(Vector3 direction, float strength = 0.12f) =>
            Context.PlayCameraKick(-direction.normalized * strength, 0.18f);

        // ------------------------------------------------ 이펙트

        public void PlayCast(Vector3 direction) => Play(Skill.CastFx, Position + BodyHeight + direction * 0.6f, direction);

        // 터지는 자리: 스킬이 정한 프리팹 + 기본 이펙트
        public void PlayImpact(Vector3 ground, float radius)
        {
            Play(Skill.ImpactFx, ground, Vector3.forward);

            switch (Skill.Style)
            {
                case ElementSkillBase.ImpactStyle.Explosion:
                    Fx.Explosion(ground, radius, Color);
                    break;
                case ElementSkillBase.ImpactStyle.Slam:
                    Fx.GroundSlam(ground, Color, radius);
                    break;
                case ElementSkillBase.ImpactStyle.Shockwave:
                    Fx.Shockwave(ground, Color, radius);
                    break;
                case ElementSkillBase.ImpactStyle.Lightning:
                    Fx.Beam(ground + Vector3.up * 9f, ground, Color, 0.35f, 0.25f);
                    Fx.Shockwave(ground, Color, radius);
                    break;
            }
        }

        public void Play(FxSlot slot, Vector3 position, Vector3 direction)
        {
            if (slot != null && slot.IsSet)
                Fx.Spawn(slot.prefab, position, Facing(direction), 1f, TintOf(slot));
        }

        // 붙잡아두는 이펙트 (예고, 버프 오라). 다 쓰면 Kill() 해줘야 함
        public FxHandle Hold(FxSlot slot, Vector3 position, Vector3 direction) =>
            slot != null && slot.IsSet ? Fx.HoldPrefab(slot.prefab, position, Facing(direction), 1f, TintOf(slot)) : null;

        public Color TintOf(FxSlot slot) => slot.tint ? Color : Color.white;

        // ------------------------------------------------ 위치

        // 커서 쪽 방향 + 사거리 안으로 자른 거리
        public Vector3 Aim(float range, out float distance) => SkillAim.Resolve(Context, 0f, range, out distance);

        // 커서 위치 (사거리 안으로). aroundSelf 면 내 자리
        public Vector3 Target(float range, bool aroundSelf)
        {
            if (aroundSelf)
                return Position;
            Vector3 direction = Aim(range, out float distance);
            return Position + direction * distance;
        }

        public Vector3 Ground(Vector3 point) =>
            Context.TryFindGround(point + Vector3.up, out Vector3 ground) ? ground : point;

        public static Vector3 PositionOf(IDamageable target, Vector3 fallback) =>
            target is Component component ? component.transform.position : fallback;

        private static Quaternion Facing(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        }

        private static float LongestCrowdControl(IReadOnlyList<HitEffect> effects)
        {
            float longest = 0f;
            foreach (HitEffect effect in effects)
            {
                if (effect.kind is HitEffectKind.Slow or HitEffectKind.Root or HitEffectKind.Stun)
                    longest = Mathf.Max(longest, effect.duration);
            }
            return longest;
        }
    }
}
