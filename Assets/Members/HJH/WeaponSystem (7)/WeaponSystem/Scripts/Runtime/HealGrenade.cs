using UnityEngine;

namespace RobotWeapons
{
    public class HealGrenade : MonoBehaviour
    {
        [SerializeField] private ParticleSystem explosionEffectPrefab;
        [SerializeField] private float editorPreviewRadius = 3f;

        private float damage, heal, radius;
        private IWeaponOwner thrower;

        public void Init(float dmg, float healAmount, float aoeRadius, float fuseTime, IWeaponOwner owner)
        {
            damage = dmg;
            heal = healAmount;
            radius = aoeRadius;
            thrower = owner;
            Invoke(nameof(Explode), fuseTime);
        }

        private void Explode()
        {
            foreach (var col in Physics.OverlapSphere(transform.position, radius))
            {
                if (col.TryGetComponent<IHealable>(out var ally) && ally.IsAlive)
                    thrower?.ApplyHealTo(ally, heal);
                if (col.TryGetComponent<IDamageable>(out var enemy) && enemy.IsAlive)
                    thrower?.ApplyDamageTo(enemy, damage);
            }

            if (explosionEffectPrefab != null)
            {
                var fx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, fx.main.duration);
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            float r = radius > 0f ? radius : editorPreviewRadius;
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, r);
        }
    }
}
