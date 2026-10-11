using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Hit reaction: knocked back a little and squashed, then springs back. Offsets are applied as
    // deltas, so other movement during the punch (pulls, AI) is kept. NavMesh agents get a real push instead.
    internal sealed class PunchFx : FxInstance
    {
        private const float Duration = 0.18f;

        private static readonly Dictionary<Transform, PunchFx> Active = new();

        private readonly Transform _target;
        private readonly Vector3 _baseScale;
        private readonly bool _agent;
        private Vector3 _direction;
        private float _strength;
        private Vector3 _applied;
        private float _time;

        private PunchFx(Transform target)
        {
            _target = target;
            _baseScale = target.localScale;
            _agent = target.TryGetComponent(out NavMeshAgent agent) && agent.enabled;
        }

        public static void Play(Transform target, Vector3 direction, float strength)
        {
            if (target == null)
                return;

            if (!Active.TryGetValue(target, out PunchFx punch) || punch._target == null)
            {
                punch = new PunchFx(target);
                Active[target] = punch;
                FxRunner.Instance.Add(punch);
            }

            direction.y = 0f;
            punch._direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            punch._strength = Mathf.Max(punch._time < Duration ? punch._strength * (1f - punch._time / Duration) : 0f, strength);
            punch._time = 0f;

            if (punch._agent && target.TryGetComponent(out NavMeshAgent navAgent) && navAgent.isOnNavMesh)
                navAgent.Move(punch._direction * strength * 0.6f);
        }

        public override bool Tick(float deltaTime)
        {
            if (_target == null)
                return false;

            _time += deltaTime;
            float t = Mathf.Clamp01(_time / Duration);

            float knock = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            if (!_agent)
            {
                Vector3 offset = _direction * _strength * knock;
                _target.position += offset - _applied;
                _applied = offset;
            }

            float squash = _strength * 0.6f * (1f - t);
            _target.localScale = new Vector3(_baseScale.x * (1f + squash), _baseScale.y * (1f - squash * 0.8f), _baseScale.z * (1f + squash));

            return t < 1f;
        }

        public override void Release()
        {
            if (_target != null)
            {
                _target.position -= _applied;
                _target.localScale = _baseScale;
            }

            if (Active.TryGetValue(_target, out PunchFx current) && current == this)
                Active.Remove(_target);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();
    }
}
