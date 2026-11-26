using System.Collections.Generic;
using Members.JJH._02_Scripts.Agents.Enemies;
using Members.KYR._01.Scripts.Player;
using RobotWeapons;
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Members.HJH._02.Scripts.Weapon.Runtime
{
    [DefaultExecutionOrder(1020)]
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
        private bool _immovable;

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
            _immovable = CrowdControl.IsImmovable(this);
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
                SetBody(true);
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

            if (_immovable)
            {
                if (_playerBody.enabled)
                    _playerBody.Move(-away * (minDistance - distance));
                _agent.velocity = Vector3.zero;
                return;
            }

            _agent.Move(away * (minDistance - distance));
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
