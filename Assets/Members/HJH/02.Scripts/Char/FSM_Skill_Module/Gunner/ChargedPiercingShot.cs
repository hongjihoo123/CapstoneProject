using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Effects;
using Assets.Members.HJH._02.Scripts.Char.Visual;
using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner
{
    public sealed class ChargedPiercingShot : ISkillTimedEffect
    {
        [System.Serializable]
        public struct Settings
        {
            public float range;
            public float width;
            [Tooltip("쏘기 전 충전 시간. 애니 이벤트로 쏘는 총이면 충전 이펙트 차오르는 시간으로만 씀")] public float windup;
            public float damage;
            [Tooltip("맞은 적 밀어내는 거리")] public float knockback;
            [Tooltip("쏘는 사람 반동 거리")] public float recoil;
            public float hitStop;
            [Tooltip("충전 중 떨림 (점점 커짐)")] public float chargeRumble;
            public float fireShake;
            [Tooltip("쏜 반대쪽으로 카메라 밀림")] public float cameraKick;
            public Color color;
        }

        private readonly ISkillContext _context;
        private Settings _s;
        private readonly Vector3 _direction;
        private readonly Vector3 _muzzleOffset;
        private readonly System.Func<Vector3> _muzzle;
        private float _elapsed;
        private FxHandle _charge;
        private FxHandle _aimLine;
        private IAnimationTimedShots _fireFrameSource;
        private bool _fireFrameReached;
        private readonly List<Vector3> _path = new();
        private readonly HashSet<IDamageable> _struck = new();

        public ChargedPiercingShot(ISkillContext context, Vector3 muzzle, Vector3 direction, Settings settings,
            System.Func<Vector3> liveMuzzle = null, IAnimationTimedShots fireFrameSource = null)
        {
            _context = context;
            _s = settings;
            _s.width *= SkillUpgrades.Multiplier(SkillUpgradeKind.Range, SkillUpgradeScope.Charged);
            _muzzle = liveMuzzle;
            direction.y = 0f;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : context.AimDirection;
            _muzzleOffset = muzzle - context.Transform.position;

            _context.LockAim(_direction, lockMove: true);

            if (fireFrameSource is { WaitsForAnimation: true })
            {
                _fireFrameSource = fireFrameSource;
                _fireFrameSource.FireFrame += OnFireFrame;
            }
        }

        private const float SafetyTimeout = 2f;

        private const float ExplosionRadius = 2.2f;
        private const float HitPadding = 0.5f;
        private const float HitHeightReach = 2.5f;

        private bool ReadyToFire => _fireFrameSource != null
            ? _fireFrameReached || _elapsed >= SafetyTimeout
            : _elapsed >= _s.windup;

        private void OnFireFrame() => _fireFrameReached = true;

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            Vector3 start = _muzzle != null ? _muzzle() : _context.Transform.position + _muzzleOffset;
            bool blocked = GroundTrace.Trace(_context, start, _direction, _s.range, _path);
            Vector3 end = _path[_path.Count - 1];

            if (!ReadyToFire)
            {
                float charge = Mathf.Clamp01(_elapsed / Mathf.Max(0.0001f, _s.windup));
                _charge ??= Fx.Charge(start, _s.color);
                _aimLine ??= Fx.AimLine(start, end, _s.color, 0.04f);
                _charge.MoveTo(start);
                _charge.SetValue(charge);
                _aimLine.SetPoints(start, end);
                _context.PlayShake(_s.chargeRumble * charge, 0.06f);
                return true;
            }

            if (_fireFrameSource != null)
                _fireFrameSource.FireFrame -= OnFireFrame;
            _fireFrameSource = null;

            StopCharge();
            _context.ReleaseAim();
            Fire(start, end, blocked);
            return false;
        }

        private void StopCharge()
        {
            _charge?.Kill();
            _aimLine?.Kill();
            _charge = null;
            _aimLine = null;
        }

        private void Fire(Vector3 start, Vector3 end, bool blocked)
        {
            Vector3 along = end - start;
            AttackAreaBus.Raise(AttackArea.Box(AttackAreaKind.Skill, (start + end) * 0.5f,
                Quaternion.LookRotation(along.sqrMagnitude > 0.0001f ? along : _direction),
                new Vector3(_s.width, 0.05f, along.magnitude), 0.15f));

            Fx.Beam(start, end, _s.color, _s.width * 1.1f, 0.3f);
            Fx.Shockwave(start - Vector3.up * 0.9f, _s.color, 1.4f);
            GunnerFx.ChargedShot(start, end, _s.color, _s.width);

            if (blocked)
            {
                Vector3 back = -_direction;
                GunnerFx.BlastHit(end, back, _s.color);
                Fx.ProjectileImpact(end, _direction, _s.color, Mathf.Max(0.8f, _s.width * 1.5f), false);
                GunnerFx.Flash(end + back * 0.3f, _s.color, 4f, 3f, 0.1f);
            }

            float radius = _s.width * 0.5f + HitPadding;
            float minY = start.y, maxY = start.y;
            foreach (Vector3 point in _path)
            {
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            int hits = 0;
            float pierceBonus = SkillUpgrades.Exact(SkillUpgradeKind.PierceDamage, SkillUpgradeScope.Charged);
            float explode = SkillUpgrades.Exact(SkillUpgradeKind.ChargeExplosion, SkillUpgradeScope.Charged);
            _struck.Clear();
            for (float y = minY - HitHeightReach; y <= maxY + HitHeightReach; y += radius)
            {
                Vector3 from = new(start.x, y, start.z);
                Vector3 to = new(end.x, y, end.z);
                foreach (IDamageable target in _context.OverlapCapsule(from, to, radius))
                {
                    if (!_struck.Add(target))
                        continue;

                    _context.DealDamage(target, _s.damage * (1f + pierceBonus * hits));
                    if (target is Component hitPart)
                        GunnerFx.BlastHit(hitPart.transform.position + Vector3.up, _direction, _s.color);
                    _context.Knockback(target, _direction, _s.knockback);
                    if (explode > 0f && target is Component blastAt)
                    {
                        SkillUpgradeScope prev = CritSystem.CurrentSlot;
                        CritSystem.CurrentSlot = SkillUpgradeScope.Charged;
                        try { SkillBlast.Explode(_context, blastAt.transform.position, ExplosionRadius, _s.damage * explode, _s.color, 0.02f, 0.15f, 0.8f); }
                        finally { CritSystem.CurrentSlot = prev; }
                    }
                    hits++;
                }
            }

            if (_s.recoil > 0f)
                _context.Dash(-_direction, _s.recoil / 0.08f, 0.08f, 0f, faceDirection: false);
            _context.PlayShake(_s.fireShake, 0.25f);
            _context.PlayCameraKick(-_direction * _s.cameraKick);
            _context.PlayHitFeel(hits > 0 ? _s.hitStop : 0f, 0f);
        }
    }
}
