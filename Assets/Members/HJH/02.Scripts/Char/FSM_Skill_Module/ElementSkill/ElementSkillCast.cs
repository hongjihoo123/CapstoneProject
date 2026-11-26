using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.ElementSkill
{
    public sealed class ElementSkillCast
    {
        private static readonly Vector3 BodyHeight = Vector3.up * 1.1f;
        private static readonly Vector3 HeadHeight = Vector3.up * 2.1f;

        private readonly HashSet<IDamageable> _hit = new();
        private readonly float _statusTime;

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

        public bool Hit(IDamageable target, Vector3 push)
        {
            if (!_hit.Add(target))
                return false;
            HitAgain(target, push);
            return true;
        }

        public void HitAgain(IDamageable target, Vector3 push)
        {
            HitTint.Mark(target, Color);
            SkillEffects.Apply(Context, Skill, Skill.OnHit, target, push);
            if (target is not Component component)
                return;

            Transform body = target is IFxBody redirect && redirect.FxBody != null ? redirect.FxBody : component.transform;
            Play(Skill.HitFx, body.position + BodyHeight, push, 1.6f);
            Flair(() => ElementFlair.Hit(Skill.Element, body.position + BodyHeight, push, Color));

            if (_statusTime > 0f && Skill.StatusFx.IsSet && target.IsAlive)
            {
                FxHandle status = Hold(Skill.StatusFx, body.position + HeadHeight, Vector3.forward);
                Fx.FollowFor(status, body, HeadHeight, _statusTime);
            }
        }

        public void ApplySelf(IReadOnlyList<SelfEffect> effects, FxSlot fx = null)
        {
            if (effects.Count == 0)
                return;
            SkillEffects.ApplySelf(Context, effects, this);
            if (fx != null)
                Play(fx, Position + BodyHeight, Context.AimDirection);
        }

        public int Impact(Vector3 center, float radius)
        {
            radius *= SkillUpgrades.Multiplier(SkillUpgradeKind.Range, Skill);
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

        public void Feel(int hits, bool groundSlam = false, float size = 1f)
        {
            if (hits <= 0)
            {
                Context.PlayHitFeel(0f, Skill.Shake * 0.4f);
                return;
            }

            float more = 1f + 0.2f * (Mathf.Min(hits, 5) - 1);
            Context.PlayHitFeel(Skill.HitStop * more, Skill.Shake * more);
            Flair(() => ElementFlair.Landed(Color, Skill.Shake, hits));
            if (Skill.Shake >= 0.5f)
                Flair(() => ScreenPulse.ImpactFrame());
            if (groundSlam)
            {
                Context.PlayCameraKick(Vector3.down * Mathf.Clamp(0.08f + size * 0.04f, 0.1f, 0.3f), 0.25f);
                return;
            }

            Vector3 aim = Context.AimDirection;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.0001f)
                Context.PlayCameraKick(aim.normalized * Mathf.Clamp(0.04f + Skill.Shake * 0.3f * more, 0.05f, 0.22f), 0.16f);
        }

        public void Recoil(Vector3 direction, float strength = 0.12f) =>
            Context.PlayCameraKick(-direction.normalized * strength, 0.18f);

        public void PlayCast(Vector3 direction)
        {
            Vector3 at = Position + BodyHeight + direction * 0.6f;
            Play(Skill.CastFx, at, direction, 1.6f);
            Flair(() => ElementFlair.Cast(Skill.Element, at, direction, Color));

            if (Skill.CastShake > 0f)
            {
                Context.PlayShake(Skill.CastShake, 0.22f);
                HitFeel.Zoom(Mathf.Clamp(Skill.CastShake * 0.25f, 0.02f, 0.08f), 0.3f);
            }
        }

        private static void Flair(System.Action draw)
        {
            try
            {
                draw();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void PlayImpact(Vector3 ground, float radius)
        {
            Play(Skill.ImpactFx, ground, Vector3.forward, radius / AuthoredRadius * 1.3f);
            Flair(() => ElementFlair.Impact(Skill.Element, ground, radius, Color));

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

        public const float AuthoredRadius = 2f;

        public static event System.Action<ElementSkillBase, FxSlot, Vector3> FxPlayed;

        public void Play(FxSlot slot, Vector3 position, Vector3 direction, float scale = 1f)
        {
            if (slot == null || !slot.IsSet)
                return;
            Fx.Spawn(slot.prefab, position, Facing(direction), scale, TintOf(slot));
            NotifyFx(slot, position);
        }

        public FxHandle Hold(FxSlot slot, Vector3 position, Vector3 direction, float scale = 1f)
        {
            if (slot == null || !slot.IsSet)
                return null;
            FxHandle handle = Fx.HoldPrefab(slot.prefab, position, Facing(direction), scale, TintOf(slot));
            NotifyFx(slot, position);
            return handle;
        }

        private void NotifyFx(FxSlot slot, Vector3 position)
        {
            if (FxPlayed == null)
                return;
            try { FxPlayed(Skill, slot, position); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        public Color TintOf(FxSlot slot) => slot.tint ? Color : Color.white;

        public Vector3 Aim(float range, out float distance) => SkillAim.Resolve(Context, 0f, range, out distance);

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
