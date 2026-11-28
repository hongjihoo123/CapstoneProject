using UnityEngine;

namespace HJH.Fx
{
    public class EnemyFxProjectile : MonoBehaviour
    {
        private ParticleSystem[] _particles;
        private TrailRenderer[] _trails;
        private Vector3 _lastPosition;
        private bool _restart;

        private void Awake()
        {
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _trails = GetComponentsInChildren<TrailRenderer>(true);
        }

        private void OnEnable()
        {
            _restart = true;
            transform.localRotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            Vector3 position = transform.position;
            if (_restart)
            {
                _restart = false;
                foreach (ParticleSystem particle in _particles)
                {
                    particle.Clear(false);
                    particle.Play(false);
                }
                foreach (TrailRenderer trail in _trails) trail.Clear();
            }
            else
            {
                Vector3 move = position - _lastPosition;
                if (move.sqrMagnitude > 0.00001f) transform.rotation = Quaternion.LookRotation(move);
            }

            _lastPosition = position;
        }
    }
}
