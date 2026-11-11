using RobotWeapons;
using System.Collections.Generic;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles
{
    [RequireComponent(typeof(SphereCollider))]
    public class BossPoisonArea : MonoBehaviour
    {
        [SerializeField] private float damageInterval = 0.5f;

        private SphereCollider _collider;
        private readonly Dictionary<IDamageable, float> _nextDamageTimes = new();

        private GameObject _owner;
        private float _damage;
        private float _duration;

        private void Awake()
        {
            _collider = GetComponent<SphereCollider>();
            _collider.isTrigger = true;
        }

        public void Initialize(GameObject owner, float radius, float damage,
                                            float duration, float interval)
        {
            _owner = owner;
            _damage = damage;
            _duration = duration;
            damageInterval = interval;

            _collider.radius = radius;

            Destroy(gameObject, _duration);
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.transform.IsChildOf(_owner.transform))
                return;

            if (!other.TryGetComponent<IDamageable>(out var damageable))
                damageable = other.GetComponentInParent<IDamageable>();

            if (damageable == null || !damageable.IsAlive)
                return;

            if (_nextDamageTimes.TryGetValue(damageable, out float nextTime)
                && Time.time < nextTime)
                return;

            damageable.TakeDamage(_damage, _owner);
            _nextDamageTimes[damageable] = Time.time + damageInterval;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent<IDamageable>(out var damageable))
                damageable = other.GetComponentInParent<IDamageable>();

            if (damageable != null)
                _nextDamageTimes.Remove(damageable);
        }
    }
}