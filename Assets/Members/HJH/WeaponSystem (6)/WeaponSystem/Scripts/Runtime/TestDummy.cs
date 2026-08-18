using UnityEngine;

namespace RobotWeapons
{
    public class TestDummy : MonoBehaviour, IDamageable, IHealable
    {
        [SerializeField] private float maxHP = 50f;
        [SerializeField] private float flashDuration = 0.15f;

        private float currentHP;
        private Renderer rend;
        private Color originalColor;
        private float flashTimer;

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
                if (rend != null) rend.material.color = Color.gray;
                Debug.Log($"[TestDummy] {gameObject.name} 파괴됨");
            }
        }

        private void Update()
        {
            if (flashTimer <= 0f || !IsAlive) return;

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
