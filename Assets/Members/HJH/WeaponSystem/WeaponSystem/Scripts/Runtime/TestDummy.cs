using UnityEngine;

namespace RobotWeapons
{
    public class TestDummy : MonoBehaviour, IDamageable, IHealable, IHitEffectSource
    {
        [SerializeField] private float maxHP = 50f;
        [SerializeField] private float flashDuration = 0.15f;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField, Tooltip("Seconds until the dummy revives by itself. 0 = only by ResetDummy (T).")]
        private float respawnDelay = 3f;

        public GameObject HitEffectPrefab => hitEffectPrefab;

        private float currentHP;
        private Renderer rend;
        private Color originalColor;
        private float flashTimer;
        private float respawnTimer;

        public bool IsAlive => currentHP > 0f;

        private void Awake()
        {
            currentHP = maxHP;
            rend = GetComponent<Renderer>();
            if (rend != null) originalColor = rend.material.color;
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            currentHP = Mathf.Min(maxHP, currentHP + amount);
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (!IsAlive) return;

            currentHP -= amount;
            Debug.Log($"[TestDummy] {gameObject.name} 이(가) {amount:F1} 데미지를 입음 (남은 HP: {Mathf.Max(currentHP, 0):F1})");

            flashTimer = flashDuration;
            if (rend != null) rend.material.color = Color.red;

            if (currentHP <= 0f)
            {
                currentHP = 0f;
                respawnTimer = respawnDelay;
                if (rend != null) rend.material.color = Color.gray;
                Debug.Log($"[TestDummy] {gameObject.name} 파괴됨");

                // Same kill report as JJH Agent, so on-kill passives can be tested on dummies.
                if (source != null && source.TryGetComponent(out Members.KYR._01_Scripts.PlayerAgent killer))
                    killer.OnEnemyKilled();
            }
        }

        private void Update()
        {
            if (!IsAlive)
            {
                if (respawnDelay > 0f)
                {
                    respawnTimer -= Time.deltaTime;
                    if (respawnTimer <= 0f)
                        ResetDummy();
                }
                return;
            }

            if (flashTimer <= 0f) return;

            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && rend != null)
                rend.material.color = originalColor;
        }

        public void ResetDummy()
        {
            currentHP = maxHP;
            if (rend != null) rend.material.color = originalColor;
        }
    }
}
