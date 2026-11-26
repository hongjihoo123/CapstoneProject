using System;
using UnityEngine;
using UnityEngine.AI;

namespace RobotWeapons
{
    [DefaultExecutionOrder(1000)]
    public class CrowdControlReceiver : MonoBehaviour, ICrowdControlTarget
    {
        public enum Tier { Normal, Elite, Boss }

        [SerializeField] private Tier tier = Tier.Normal;
        [SerializeField, Tooltip("기절 끝나고 기절 안 걸리는 시간")] private float stunImmunity = 1f;
        [SerializeField, Tooltip("밀려나는 데 걸리는 시간")] private float knockbackTime = 0.15f;

        [Header("경직 (맞을 때마다 잠깐 멈칫, 연타하면 이어짐)")]
        [SerializeField, Tooltip("연타로 경직이 이어질 수 있는 최대 시간. 넘으면 잠깐 경직 면역 (무한 경직 방지)")]
        private float flinchChainMax = 0.6f;
        [SerializeField, Tooltip("경직이 최대까지 이어진 뒤 면역 시간")] private float flinchImmunity = 0.5f;
        [SerializeField, Range(0f, 1f), Tooltip("경직 중 애니메이션 속도 (0 = 완전 정지)")]
        private float flinchAnimSpeed = 0.15f;

        private float _slowEnd;
        private float _slowPercent;
        private float _rootEnd;
        private float _stunEnd;
        private float _stunImmuneEnd;
        private Vector3 _knockback;
        private float _knockbackSpeed;

        private float _flinchEnd;
        private float _flinchChainStart;
        private float _flinchImmuneEnd;
        private Animator _animator;
        private float _animBaseSpeed = 1f;
        private bool _animSlowed;

        private NavMeshAgent _agent;
        private float _baseSpeed;
        private bool _wasStopped;
        private bool _slowing;

        public event Action<CrowdControlType, bool> Changed;

        public Tier EnemyTier { get => tier; set => tier = value; }

        public static event Action<CrowdControlReceiver> Awakened;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            Awakened?.Invoke(this);
        }

        public bool Has(CrowdControlType type) => type switch
        {
            CrowdControlType.Slow => Time.time < _slowEnd,
            CrowdControlType.Root => Time.time < _rootEnd,
            CrowdControlType.Stun => Time.time < _stunEnd,
            CrowdControlType.Knockback => _knockback.sqrMagnitude > 0.0001f,
            _ => false
        };

        public void ApplySlow(float percent, float duration)
        {
            if (tier == Tier.Boss)
                return;

            bool had = Has(CrowdControlType.Slow);
            _slowEnd = Mathf.Max(_slowEnd, Time.time + duration);
            _slowPercent = had ? Mathf.Max(_slowPercent, percent) : percent;
            if (!had)
                Changed?.Invoke(CrowdControlType.Slow, true);
        }

        public void ApplyRoot(float duration)
        {
            if (!Scale(ref duration))
                return;

            bool had = Has(CrowdControlType.Root);
            _rootEnd = Mathf.Max(_rootEnd, Time.time + duration);
            if (!had)
                Changed?.Invoke(CrowdControlType.Root, true);
        }

        public void ApplyStun(float duration)
        {
            if (tier == Tier.Boss)
                return;

            if (!Scale(ref duration) || Time.time < _stunImmuneEnd)
                return;

            bool had = Has(CrowdControlType.Stun);
            _stunEnd = Mathf.Max(_stunEnd, Time.time + duration);
            _stunImmuneEnd = _stunEnd + stunImmunity;
            if (!had)
                Changed?.Invoke(CrowdControlType.Stun, true);
        }

        public void ApplyKnockback(Vector3 direction, float distance)
        {
            if (tier == Tier.Boss || CrowdControl.IsImmovable(this))
                return;
            if (tier == Tier.Elite)
                distance *= 0.5f;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || distance <= 0f)
                return;

            Vector3 push = direction.normalized * distance;
            if (push.sqrMagnitude <= _knockback.sqrMagnitude)
                return;

            bool had = Has(CrowdControlType.Knockback);
            _knockback = push;
            _knockbackSpeed = distance / Mathf.Max(0.01f, knockbackTime);
            if (!had)
                Changed?.Invoke(CrowdControlType.Knockback, true);
        }

        private float _interruptEnd;

        public bool IsInterrupted => Has(CrowdControlType.Stun) || Has(CrowdControlType.Knockback) || Time.time < _interruptEnd;

        public void Interrupt(float duration)
        {
            if (tier == Tier.Boss)
                return;

            if (!Scale(ref duration))
                return;
            _interruptEnd = Mathf.Max(_interruptEnd, Time.time + duration);
        }

        public bool IsFlinching => Time.time < _flinchEnd;

        public void ApplyFlinch(float duration)
        {
            if (tier == Tier.Boss)
                return;

            if (!Scale(ref duration) || Time.time < _flinchImmuneEnd)
                return;

            float now = Time.time;
            if (now >= _flinchEnd)
                _flinchChainStart = now;

            float chainEnd = _flinchChainStart + flinchChainMax;
            _flinchEnd = Mathf.Min(Mathf.Max(_flinchEnd, now + duration), chainEnd);
            if (_flinchEnd >= chainEnd - 0.0001f)
                _flinchImmuneEnd = chainEnd + flinchImmunity;
        }

        private bool Scale(ref float duration)
        {
            if (tier == Tier.Boss)
                return false;
            if (tier == Tier.Elite)
                duration *= 0.5f;
            return duration > 0f;
        }

        private void Update()
        {
            UpdateKnockback();
            CheckEnded(CrowdControlType.Slow, ref _slowEnd);
            CheckEnded(CrowdControlType.Root, ref _rootEnd);
            CheckEnded(CrowdControlType.Stun, ref _stunEnd);
            ApplyToAgent();
            ApplyFlinchAnimation();
        }

        private void OnDisable() => RestoreAnimation();

        private void ApplyFlinchAnimation()
        {
            bool flinching = IsFlinching;
            if (flinching && !_animSlowed)
            {
                if (_animator == null)
                    _animator = GetComponentInChildren<Animator>();
                if (_animator == null)
                    return;
                _animBaseSpeed = _animator.speed;
                _animator.speed = _animBaseSpeed * flinchAnimSpeed;
                _animSlowed = true;
            }
            else if (!flinching)
            {
                RestoreAnimation();
            }
        }

        private void RestoreAnimation()
        {
            if (!_animSlowed)
                return;
            if (_animator != null)
                _animator.speed = _animBaseSpeed;
            _animSlowed = false;
        }

        private void UpdateKnockback()
        {
            if (_knockback.sqrMagnitude <= 0.0001f)
                return;

            float step = Mathf.Min(_knockbackSpeed * Time.deltaTime, _knockback.magnitude);
            Vector3 delta = _knockback.normalized * step;
            Move(delta);
            _knockback -= delta;

            if (_knockback.sqrMagnitude <= 0.0001f)
            {
                _knockback = Vector3.zero;
                Changed?.Invoke(CrowdControlType.Knockback, false);
            }
        }

        private void CheckEnded(CrowdControlType type, ref float end)
        {
            if (end > 0f && Time.time >= end)
            {
                end = 0f;
                Changed?.Invoke(type, false);
            }
        }

        private void ApplyToAgent()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            bool stopped = IsInterrupted || Has(CrowdControlType.Root) || IsFlinching;
            if (stopped)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
            else if (_wasStopped)
            {
                _agent.isStopped = false;
            }
            _wasStopped = stopped;

            bool slowed = Has(CrowdControlType.Slow);
            if (slowed && !_slowing)
                _baseSpeed = _agent.speed;
            if (slowed)
                _agent.speed = _baseSpeed * (1f - Mathf.Clamp01(_slowPercent / 100f));
            else if (_slowing)
                _agent.speed = _baseSpeed;
            _slowing = slowed;
        }

        private void Move(Vector3 delta)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.Move(delta);
            else if (TryGetComponent(out CharacterController controller) && controller.enabled)
                controller.Move(delta);
            else if (TryGetComponent(out Rigidbody body) && !body.isKinematic)
                body.MovePosition(body.position + delta);
            else
                transform.position += delta;
        }
    }
}
