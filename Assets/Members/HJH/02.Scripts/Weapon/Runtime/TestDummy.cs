using Members.KYR._01.Scripts.Player;
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

        [Header("CC 걸린 동안 색 (여러 개면 위에 있는 게 우선)")]
        [SerializeField] private Color stunColor = new Color(1f, 0.85f, 0.1f);    // 기절: 노랑
        [SerializeField] private Color rootColor = new Color(0.2f, 0.9f, 0.4f);   // 속박: 초록
        [SerializeField] private Color knockbackColor = Color.white;              // 밀치기: 흰색
        [SerializeField] private Color slowColor = new Color(0.3f, 0.6f, 1f);     // 둔화: 파랑

        public GameObject HitEffectPrefab => hitEffectPrefab;

        private float currentHP;
        private Renderer rend;
        private Color originalColor;
        private float flashTimer;
        private float respawnTimer;
        private CrowdControlReceiver cc;

        public bool IsAlive => currentHP > 0f;

        private void Awake()
        {
            currentHP = maxHP;
            rend = GetComponent<Renderer>();
            if (rend != null) originalColor = rend.material.color;

            // 첫 CC 맞을 때 자동으로 붙긴 하지만, 테스트할 땐 처음부터 보이게 미리 붙여 둠
            if (!TryGetComponent(out cc))
                cc = gameObject.AddComponent<CrowdControlReceiver>();
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
                if (source != null && source.TryGetComponent(out PlayerAgent killer))
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

            // 맞은 빨간 깜빡임이 먼저, 끝나면 CC 색 (없으면 원래 색)
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer > 0f) return;
            }

            if (rend != null) rend.material.color = IdleColor();
        }

        private Color IdleColor()
        {
            if (cc == null) return originalColor;
            if (cc.Has(CrowdControlType.Stun)) return stunColor;
            if (cc.Has(CrowdControlType.Root)) return rootColor;
            if (cc.Has(CrowdControlType.Knockback)) return knockbackColor;
            if (cc.Has(CrowdControlType.Slow)) return slowColor;
            return originalColor;
        }

        public void ResetDummy()
        {
            currentHP = maxHP;
            if (rend != null) rend.material.color = IdleColor();
        }
    }
}
