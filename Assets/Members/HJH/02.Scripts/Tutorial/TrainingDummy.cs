using Members.JJH._02_Scripts.Agents.Enemies;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    public class TrainingDummy : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField, Tooltip("맞을 때 기우는 힘")] private float kick = 170f;
        [SerializeField] private float stiffness = 160f;
        [SerializeField] private float damping = 8f;
        [SerializeField, Tooltip("쓰러지는 시간")] private float fallTime = 0.4f;

        private AbstractEnemy _enemy;
        private Transform _player;
        private float _lastHealth = -1f;
        private float _angle;
        private float _speed;
        private float _fall;
        private Vector3 _axis = Vector3.right;

        private void Awake() => _enemy = GetComponent<AbstractEnemy>();

        private void Update()
        {
            if (_enemy == null || visual == null || _enemy.Health == null)
                return;

            float health = _enemy.Health.CurrentHealth;
            if (_lastHealth < 0f)
            {
                if (health > 0f)
                    _lastHealth = health;
                return;
            }

            if (health < _lastHealth)
            {
                _axis = TiltAxis();
                _speed = Mathf.Min(_speed + kick, kick * 1.5f);
            }
            _lastHealth = health;

            float dt = Time.deltaTime;
            if (health <= 0f)
            {
                _fall = Mathf.Min(1f, _fall + dt / Mathf.Max(0.05f, fallTime));
                _angle = Mathf.Lerp(_angle, 85f, _fall * _fall);
            }
            else
            {
                _speed += (-_angle * stiffness - _speed * damping) * dt;
                _angle += _speed * dt;
            }

            visual.localRotation = Quaternion.AngleAxis(_angle, _axis);
        }

        private Vector3 TiltAxis()
        {
            if (_player == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                _player = player != null ? player.transform : null;
            }

            Vector3 away = _player != null ? transform.position - _player.position : transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = transform.forward;
            return transform.InverseTransformDirection(Vector3.Cross(Vector3.up, away.normalized));
        }
    }
}
