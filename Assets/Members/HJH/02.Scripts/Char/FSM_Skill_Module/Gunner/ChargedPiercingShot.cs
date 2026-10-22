using Assets.Members.HJH._02.Scripts.Char.Visual;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.Gunner
{
    // 건슬링어 패시브의 강화 평타: 잠깐 충전했다가 일직선으로 관통하는 충격포.
    // 맞은 적 전부 피해 + 뒤로 밀림, 쏘는 사람도 반동으로 살짝 뒤로 밀림.
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
        private readonly Settings _s;
        private readonly Vector3 _direction;
        private readonly Vector3 _muzzleOffset;
        private readonly System.Func<Vector3> _muzzle;
        private float _elapsed;
        private FxHandle _charge;
        private FxHandle _aimLine;
        private IAnimationTimedShots _fireFrameSource;
        private bool _fireFrameReached;

        // muzzle: 총이 알려주는 "지금 총구 위치" (있으면 충전 이펙트가 총구에 딱 붙어서 따라감)
        // fireFrameSource: 애니 이벤트로 쏘는 총이면 넘김 -> 충전하다가 PassiveShot 클립의 Anim_Fire 프레임에 발사
        public ChargedPiercingShot(ISkillContext context, Vector3 muzzle, Vector3 direction, Settings settings,
            System.Func<Vector3> liveMuzzle = null, IAnimationTimedShots fireFrameSource = null)
        {
            _context = context;
            _s = settings;
            _muzzle = liveMuzzle;
            direction.y = 0f;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : context.AimDirection;
            // 총구 위치를 못 받으면, 충전하는 동안 움직여도 따라가게 플레이어 기준 위치로 기억
            _muzzleOffset = muzzle - context.Transform.position;

            if (fireFrameSource is { WaitsForAnimation: true })
            {
                _fireFrameSource = fireFrameSource;
                _fireFrameSource.FireFrame += OnFireFrame;
            }
        }

        // 애니 이벤트 안 와도 총이 알아서 시간 초과로 FireFrame 보내줌. 이건 총이 사라졌을 때 대비용
        private const float SafetyTimeout = 2f;

        // 애니 이벤트로 쏘면: 총이 FireFrame 보낼 때 / 아니면: 충전 시간 끝
        private bool ReadyToFire => _fireFrameSource != null
            ? _fireFrameReached || _elapsed >= SafetyTimeout
            : _elapsed >= _s.windup;

        private void OnFireFrame() => _fireFrameReached = true;

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            Vector3 start = _muzzle != null ? _muzzle() : _context.Transform.position + _muzzleOffset;
            Vector3 end = start + _direction * _s.range;

            if (!ReadyToFire)
            {
                // 충전: 총구에 에너지 모이고 조준선 보임
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
            Fire(start, end);
            return false;
        }

        private void StopCharge()
        {
            _charge?.Kill();
            _aimLine?.Kill();
            _charge = null;
            _aimLine = null;
        }

        private void Fire(Vector3 start, Vector3 end)
        {
            AttackAreaBus.Raise(AttackArea.Box(AttackAreaKind.Skill, (start + end) * 0.5f, Quaternion.LookRotation(_direction),
                new Vector3(_s.width, 0.05f, _s.range), 0.15f));

            Fx.Beam(start, end, _s.color, _s.width * 1.1f, 0.3f);
            Fx.Shockwave(start - Vector3.up * 0.9f, _s.color, 1.4f);

            int hits = 0;
            foreach (IDamageable target in _context.OverlapCapsule(start, end, _s.width * 0.5f))
            {
                _context.DealDamage(target, _s.damage);
                _context.Knockback(target, _direction, _s.knockback);
                hits++;
            }

            if (_s.recoil > 0f)
                _context.Dash(-_direction, _s.recoil / 0.08f, 0.08f, 0f, faceDirection: false);   // 반동이라 조준 방향 유지
            _context.PlayShake(_s.fireShake, 0.25f);
            _context.PlayCameraKick(-_direction * _s.cameraKick);
            _context.PlayHitFeel(hits > 0 ? _s.hitStop : 0f, 0f);
        }
    }
}
