using System.Collections.Generic;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Members.HJH._02.Scripts.Weapon.Runtime
{
    // 적이 플레이어를 밀지 않게 하는 어댑터 (JJH 적 코드·프리팹은 안 고침).
    //  원인: 적 NavMeshAgent 는 stoppingDistance 0 으로 플레이어 위치 "한가운데"까지 달려옴 → 물리를 무시해서 플레이어 몸과 겹침
    //        → 플레이어 CharacterController 가 겹친 걸 풀려고 매 프레임 자기를 밀어냄 = "적이 계속 민다"
    //  처리 1 (살아 있을 때): 적 몸이 플레이어 몸에 닿는 거리 안으로 들어오면 "적을" 그 거리 밖으로 되돌림 (플레이어는 안 밀림)
    //  처리 2 (죽는 중): 시체가 다시 걸어오지 않게 길찾기를 계속 멈추고, 몸 콜라이더를 꺼서 막지도 밀지도 않게
    //        (경직이 끝나면 CrowdControlReceiver 가 isStopped 를 풀어서 죽는 애니 중에 플레이어 쪽으로 미끄러지던 문제)
    // CrowdControlReceiver 가 생길 때(적 프리팹에 이미 있음) 같이 붙음
    [DefaultExecutionOrder(1020)]   // Receiver(1000) 가 isStopped 를 푼 다음에 다시 멈추게
    public sealed class EnemyBodySpacing : MonoBehaviour
    {
        [SerializeField, Tooltip("몸끼리 닿는 거리에 더하는 여유 (m)")] private float gap = 0.05f;

        private static PlayerAgent _player;
        private static CharacterController _playerBody;

        private AbstractEnemy _enemy;
        private NavMeshAgent _agent;
        private readonly List<Collider> _bodyColliders = new();
        private float _bodyRadius = 0.5f;
        private bool _bodyOff;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            _player = null;
            _playerBody = null;
            CrowdControlReceiver.Awakened -= Attach;
            CrowdControlReceiver.Awakened += Attach;
        }

        private static void Attach(CrowdControlReceiver receiver)
        {
            if (receiver.GetComponent<AbstractEnemy>() != null && receiver.GetComponent<EnemyBodySpacing>() == null)
                receiver.gameObject.AddComponent<EnemyBodySpacing>();
        }

        private void Awake()
        {
            _enemy = GetComponent<AbstractEnemy>();
            _agent = GetComponent<NavMeshAgent>();
            foreach (Collider c in GetComponents<Collider>())
            {
                if (c.isTrigger)
                    continue;
                _bodyColliders.Add(c);
            }
        }

        private void Start()
        {
            float radius = 0f;
            foreach (Collider c in _bodyColliders)
            {
                if (c is BoxCollider box)
                {
                    // 상자는 돌면 모서리가 튀어나옴 → 바닥면 대각선 절반 (어느 방향으로 돌아도 안 겹치게)
                    Vector3 s = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f;
                    radius = Mathf.Max(radius, new Vector2(s.x, s.z).magnitude);
                }
                else
                {
                    Vector3 e = c.bounds.extents;
                    radius = Mathf.Max(radius, Mathf.Max(e.x, e.z));
                }
            }
            if (radius > 0.05f)
                _bodyRadius = radius;
        }

        private void LateUpdate()
        {
            if (_enemy == null)
                return;

            if (!_enemy.IsAlive)
            {
                HoldCorpse();
                return;
            }

            if (_bodyOff)
                SetBody(true);   // 풀에서 다시 살아난 경우
            KeepOutOfPlayer();
        }

        private void HoldCorpse()
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                if (_agent.hasPath)
                    _agent.ResetPath();
            }
            if (!_bodyOff)
                SetBody(false);
        }

        private void SetBody(bool on)
        {
            foreach (Collider c in _bodyColliders)
                if (c != null)
                    c.enabled = on;
            _bodyOff = !on;
        }

        private void KeepOutOfPlayer()
        {
            if (!FindPlayer() || _agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;

            Vector3 offset = transform.position - _player.transform.position;
            offset.y = 0f;
            float minDistance = _bodyRadius + _playerBody.radius + gap;
            float distance = offset.magnitude;
            if (distance >= minDistance)
                return;

            Vector3 away = distance > 0.0001f ? offset / distance : -transform.forward;
            _agent.Move(away * (minDistance - distance));   // 적을 밖으로 (내비메시 밖으로는 안 나감)
            // 플레이어 쪽으로 가던 속도는 버림 → 다음 프레임에 다시 파고들지 않게
            Vector3 v = _agent.velocity;
            float into = Vector3.Dot(v, -away);
            if (into > 0f)
                _agent.velocity = v + away * into;
        }

        private static bool FindPlayer()
        {
            if (_player != null && _playerBody != null)
                return _player.isActiveAndEnabled;
            _player = FindFirstObjectByType<PlayerAgent>();
            _playerBody = _player != null && _player.Mover != null ? _player.Mover.Body : null;
            return _player != null && _playerBody != null;
        }
    }
}
