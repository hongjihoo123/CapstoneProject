using UnityEngine;

namespace RobotWeapons
{
    public class LaserBeamInstance : MonoBehaviour
    {
        [SerializeField] private float damageInterval = 0.2f;
        [SerializeField] private float maxDuration = 5f;
        [SerializeField] private GameObject impactEffect;

        private Transform firePoint;
        private IWeaponOwner owner;
        private float damagePerHit;
        private float range;

        private float timer;
        private float damageTimer;
        private bool stopped;

        public void Init(Transform firePoint, IWeaponOwner owner, float damagePerHit, float range)
        {
            this.firePoint = firePoint;
            this.owner = owner;
            this.damagePerHit = damagePerHit;
            this.range = range;

            if (impactEffect != null)
            {
                impactEffect.SetActive(true);
                impactEffect.BroadcastMessage("OnSpawned", SendMessageOptions.DontRequireReceiver);
            }
        }

        public void Stop()
        {
            if (stopped) return;
            stopped = true;

            if (impactEffect != null)
            {
                impactEffect.BroadcastMessage("OnDespawned", SendMessageOptions.DontRequireReceiver);
                impactEffect.SetActive(false);
            }

            Destroy(gameObject);
        }

        private void Update()
        {
            if (firePoint == null || stopped) return;

            transform.position = firePoint.position;
            transform.rotation = firePoint.rotation;

            bool hitSomething = Physics.Raycast(firePoint.position, firePoint.forward, out RaycastHit hit, range);

            if (impactEffect != null)
            {
                if (hitSomething)
                {
                    impactEffect.transform.position = hit.point;
                    impactEffect.transform.rotation = Quaternion.LookRotation(hit.normal);
                }
                else
                {
                    impactEffect.transform.position = firePoint.position + firePoint.forward * range;
                    impactEffect.transform.rotation = firePoint.rotation;
                }
            }

            timer += Time.deltaTime;
            damageTimer += Time.deltaTime;

            if (damageTimer >= damageInterval)
            {
                damageTimer -= damageInterval;
                if (hitSomething && hit.collider.TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                    owner.ApplyDamageTo(target, damagePerHit);
            }

            if (timer >= maxDuration)
                Stop();
        }
    }
}