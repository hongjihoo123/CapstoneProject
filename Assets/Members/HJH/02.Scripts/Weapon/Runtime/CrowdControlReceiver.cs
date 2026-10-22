using System;
using UnityEngine;
using UnityEngine.AI;

namespace RobotWeapons
{
    // 기본 CC 받는 쪽. 적한테 아무것도 없으면 첫 CC 맞을 때 자동으로 붙음
    //  - 같은 CC 또 걸리면 겹치지 않고 "남은 시간 vs 새 시간" 중 큰 값
    //  - 기절 끝나면 잠깐 기절 면역 (계속 묶어두고 때리기 방지)
    //  - 엘리트는 시간 절반, 보스는 속박/기절/밀치기 안 걸리고 둔화만 절반
    //  - 움직임은 NavMeshAgent 로 멈추고 느리게 함. 공격(AI) 멈추는 건 적 담당이 Changed 이벤트 받아서 처리
    // 적 AI(BT)가 같은 프레임에 isStopped 를 풀어도 이쪽이 마지막에 다시 멈추게 늦게 실행
    [DefaultExecutionOrder(1000)]
    public class CrowdControlReceiver : MonoBehaviour, ICrowdControlTarget
    {
        public enum Tier { Normal, Elite, Boss }

        [SerializeField] private Tier tier = Tier.Normal;
        [SerializeField, Tooltip("기절 끝나고 기절 안 걸리는 시간")] private float stunImmunity = 1f;
        [SerializeField, Tooltip("밀려나는 데 걸리는 시간")] private float knockbackTime = 0.15f;

        private float _slowEnd;
        private float _slowPercent;
        private float _rootEnd;
        private float _stunEnd;
        private float _stunImmuneEnd;
        private Vector3 _knockback;      // 남은 밀림 거리 (방향 포함)
        private float _knockbackSpeed;

        private NavMeshAgent _agent;
        private float _baseSpeed;
        private bool _wasStopped;
        private bool _slowing;

        // CC 가 걸리거나 풀릴 때마다. AI 멈춤, 머리 위 아이콘 같은 건 여기 구독하면 됨
        public event Action<CrowdControlType, bool> Changed;

        public Tier EnemyTier { get => tier; set => tier = value; }

        private void Awake() => _agent = GetComponent<NavMeshAgent>();

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
                percent *= 0.5f;

            bool had = Has(CrowdControlType.Slow);
            _slowEnd = Mathf.Max(_slowEnd, Time.time + duration);
            // 더 센 둔화가 들어오면 그걸로, 약한 건 시간만 늘림
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
            if (tier == Tier.Boss)
                return;
            if (tier == Tier.Elite)
                distance *= 0.5f;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || distance <= 0f)
                return;

            // 밀치기는 시간이 아니라 거리라서, 더 멀리 미는 쪽으로 덮어씀
            Vector3 push = direction.normalized * distance;
            if (push.sqrMagnitude <= _knockback.sqrMagnitude)
                return;

            bool had = Has(CrowdControlType.Knockback);
            _knockback = push;
            _knockbackSpeed = distance / Mathf.Max(0.01f, knockbackTime);
            if (!had)
                Changed?.Invoke(CrowdControlType.Knockback, true);
        }

        // 보스는 속박/기절 안 걸림, 엘리트는 절반. false = 안 걸림
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

        // 기절/속박/밀리는 중엔 길찾기 멈춤, 둔화면 속도만 줄임
        private void ApplyToAgent()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            bool stopped = Has(CrowdControlType.Stun) || Has(CrowdControlType.Root) || Has(CrowdControlType.Knockback);
            if (stopped)
            {
                // 적 AI 가 추적하면서 매 프레임 isStopped = false 로 되돌리기 때문에, 걸려 있는 동안은 매 프레임 다시 멈춤
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
            else if (_wasStopped)
            {
                _agent.isStopped = false;
            }
            _wasStopped = stopped;

            // 둔화 중일 때만 속도를 건드림 (적 AI 가 속도를 바꾸는 걸 평소엔 덮어쓰지 않게)
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
                _agent.Move(delta);   // 벽은 NavMesh 가 알아서 막아줌
            else if (TryGetComponent(out CharacterController controller) && controller.enabled)
                controller.Move(delta);
            else if (TryGetComponent(out Rigidbody body) && !body.isKinematic)
                body.MovePosition(body.position + delta);
            else
                transform.position += delta;
        }
    }
}
