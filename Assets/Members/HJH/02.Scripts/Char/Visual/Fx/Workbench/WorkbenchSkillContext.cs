using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // FxWorkbench 에서 플레이어 대신 스킬을 쓰는 "가짜 시전자".
    // 스킬 에셋의 진짜 코드(Begin → Tick → End)가 그대로 돌아가고, 세상에 묻는 것만 여기서 대답함:
    //   - 조준: 항상 작업대 앞쪽(파란 화살표)으로 Aim Distance 만큼
    //   - 바닥: 작업대 높이의 평평한 바닥, 벽 없음
    //   - 적: 허수아비(WorkbenchDummy)만. 판정 위치는 스킬 파트 쪽, 이펙트는 적 파트 쪽 몸에 뜸
    //   - 돌진/순간이동: 시전자 캡슐이 진짜로 움직임 (다음 시전 때 제자리로)
    //   - 스탯·회복·무기·패시브: 없음 (이펙트 확인용이라 무시)
    public sealed class WorkbenchSkillContext : ISkillContext
    {
        private const float BodyRadius = 0.45f;

        private readonly List<ISkillTimedEffect> _effects = new();
        private readonly List<ISkillTimedEffect> _pending = new();
        private readonly List<WorkbenchDummy> _dummies;
        private readonly Vector3 _forward;
        private readonly float _aimDistance;
        private readonly float _groundY;

        private Vector3 _dashVelocity;
        private float _dashLeft;
        private Vector3 _scriptedMotion;

        public WorkbenchSkillContext(Transform caster, Vector3 forward, float aimDistance, List<WorkbenchDummy> dummies)
        {
            Transform = caster;
            _forward = forward;
            _aimDistance = aimDistance;
            _dummies = dummies;
            _groundY = caster.position.y;
        }

        public Transform Transform { get; }
        public SkillData ActiveSkill { get; set; }
        public IPassive Passive => null;
        public IWeapon Weapon => null;
        public bool HasRunningEffects => _effects.Count > 0 || _pending.Count > 0;

        // 작업대가 매 프레임 불러줌: 돌진 이동 + 스킬이 남긴 것(투사체, 지연 폭발) 진행
        public void Tick(float deltaTime)
        {
            if (_dashLeft > 0f)
            {
                float step = Mathf.Min(deltaTime, _dashLeft);
                _dashLeft -= step;
                Transform.position += _dashVelocity * step;
            }
            else if (_scriptedMotion != Vector3.zero)
            {
                Transform.position += _scriptedMotion * deltaTime;
            }

            _effects.AddRange(_pending);
            _pending.Clear();
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (!_effects[i].Tick(deltaTime))
                    _effects.RemoveAt(i);
            }
        }

        // ---------- ISkillMover ----------
        public Vector3 MoveInputDirection => _forward;
        public Vector3 AimDirection => _forward;
        public Vector3 AimPoint => Transform.position + _forward * _aimDistance;
        public bool IsDashing => _dashLeft > 0f;

        public void Dash(Vector3 direction, float speed, float duration, float endSlowdown = 0f, bool faceDirection = true)
        {
            direction.y = 0f;
            _dashVelocity = direction.normalized * speed;
            _dashLeft = Mathf.Max(0f, duration);
            if (faceDirection && direction.sqrMagnitude > 0.0001f)
                Transform.rotation = Quaternion.LookRotation(direction);
        }

        public bool TryGetDashFacing(out Vector3 direction)
        {
            direction = _dashVelocity.normalized;
            return IsDashing;
        }

        // 워크벤치엔 마우스 조준이 없어서 잠금은 기억만 함
        private bool _aimLocked;
        private Vector3 _aimLockDirection;
        public void LockAim(Vector3 direction, bool lockMove) { _aimLocked = true; _aimLockDirection = direction; }
        public void ReleaseAim() => _aimLocked = false;
        public bool TryGetAimLock(out Vector3 direction) { direction = _aimLockDirection; return _aimLocked; }

        public void SetScriptedMotion(Vector3 velocity) => _scriptedMotion = velocity;
        public void SetScriptedPosition(Vector3 position) => Transform.position = position;

        public void CancelDash()
        {
            _dashLeft = 0f;
            _scriptedMotion = Vector3.zero;
        }

        public bool SweepEnvironment(Vector3 from, Vector3 to) => false;

        public bool TryFindGround(Vector3 from, out Vector3 ground)
        {
            ground = new Vector3(from.x, _groundY, from.z);
            return true;
        }

        public bool LineBlocked(Vector3 from, Vector3 to, out Vector3 hitPoint)
        {
            hitPoint = to;
            return false;
        }

        // ---------- ISkillEffects ----------
        public void SetSpinEffect(GameObject prefab, bool active, float radius) { }

        public void FlashRange(float radius, float duration) =>
            AttackAreaBus.Raise(AttackArea.Circle(AttackAreaKind.Skill, Transform.position + Vector3.up, radius, duration));

        public void Run(ISkillTimedEffect effect)
        {
            if (effect != null)
                _pending.Add(effect);
        }

        public void PlayHitFeel(float hitStop, float shake) => HitFeel.Play(hitStop, shake);
        public void PlayShake(float amplitude, float duration) => HitFeel.Shake(amplitude, duration);
        public void PlayCameraKick(Vector3 offset, float duration = 0.22f) => HitFeel.Kick(offset, duration);

        // ---------- ISkillCombatant: 허수아비만 맞음 ----------
        public bool TryRegisterHit(IDamageable target) => true;

        public IReadOnlyList<IDamageable> OverlapMelee() => OverlapSphere(Transform.position + _forward, 1.5f);

        public IReadOnlyList<IDamageable> OverlapSphere(Vector3 center, float radius)
        {
            var result = new List<IDamageable>();
            foreach (WorkbenchDummy dummy in _dummies)
            {
                if (Flat(dummy.transform.position - center).magnitude <= radius + BodyRadius)
                    result.Add(dummy);
            }
            return result;
        }

        public IReadOnlyList<IDamageable> OverlapCapsule(Vector3 from, Vector3 to, float radius)
        {
            var hits = new List<(float, IDamageable)>();
            Vector3 a = Flat(from), b = Flat(to);
            foreach (WorkbenchDummy dummy in _dummies)
            {
                Vector3 p = Flat(dummy.transform.position);
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                if ((a + ab * t - p).magnitude <= radius + BodyRadius)
                    hits.Add(((p - a).sqrMagnitude, dummy));
            }
            hits.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            var result = new List<IDamageable>(hits.Count);
            foreach ((float _, IDamageable target) in hits)
                result.Add(target);
            return result;
        }

        public void PushAway(Vector3 center, float radius)
        {
            foreach (IDamageable target in OverlapSphere(center, radius))
                Knockback(target, ((WorkbenchDummy)target).transform.position - center, 1.5f);
        }

        public void PullToward(Vector3 center, float radius, float maxDistance)
        {
            foreach (IDamageable target in OverlapSphere(center, radius))
                Knockback(target, center - ((WorkbenchDummy)target).transform.position, Mathf.Min(maxDistance, 1.5f));
        }

        public void Knockback(IDamageable target, Vector3 direction, float distance) =>
            (target as WorkbenchDummy)?.ApplyKnockback(direction, distance);

        public void DealDamage(IDamageable target, float amount, bool isWeakpoint = false) => target?.TakeDamage(amount, Transform.gameObject);
        public void DealDamage(IDamageable target, float amount, SkillData skill) => target?.TakeDamage(amount, Transform.gameObject);

        public void ApplyDamageOverTime(IDamageable target, float damagePerTick, float tickInterval, float duration) =>
            (target as WorkbenchDummy)?.ApplyBurn(damagePerTick, tickInterval, duration);

        // ---------- 스탯 / 회복: 이펙트 확인용이라 무시 ----------
        public bool StatsReady => false;
        public void AddStatModifier(PlayerStatId id, StatModifier modifier) { }
        public void RemoveStatModifiers(object source) { }
        public void Heal(float amount) { }
        public void AddShield(float amount) { }
        public void BoostSkillDamage(SkillData skill, float scale, float seconds) { }
        public event System.Action<float, GameObject> DamageTaken { add { } remove { } }
        public void SetIncomingDamageScale(object source, float scale) { }

        // ---------- 쿨타임: 작업대엔 다른 칸이 없음 ----------
        public void ReduceCooldown(SkillSlotId slot, float seconds) { }
        public float GetCooldownRemaining(SkillSlotId slot) => 0f;

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
