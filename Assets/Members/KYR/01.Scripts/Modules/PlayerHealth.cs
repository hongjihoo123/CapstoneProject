    using Members.JJH._02_Scripts.Systems.ModuleSystem;
    using UnityEngine;

    namespace Members.KYR._01_Scripts.Modules
    {
        public class PlayerHealth : Module, IHealth
        {
            [SerializeField] private float maxHp = 100f;
            [SerializeField] private float stunOnHitDuration;
            public float CurrentHealth => _hp;
            public float MaxHealth => maxHp;

            private float _hp;
            private float _stunRemaining;
            public bool IsDead => _hp <= 0f;
            public bool IsStunned => _stunRemaining > 0f;

            public override void Initialize(ModuleOwner owner)
            {
                base.Initialize(owner);
                _hp = maxHp;
                _stunRemaining = 0f;
            }

            public void Tick(float deltaTime)
            {
                if (_stunRemaining <= 0f)
                    return;

                _stunRemaining -= deltaTime;
                if (_stunRemaining < 0f)
                    _stunRemaining = 0f;
            }

            public void TakeDamage(float amount)
            {
                if (IsDead || amount <= 0f)
                    return;

                _hp = Mathf.Max(0f, _hp - amount);
                if (!IsDead && stunOnHitDuration > 0f)
                    ApplyStun(stunOnHitDuration);
            }

            public void Heal(float amount)
            {
                if (IsDead || amount <= 0f)
                    return;

                _hp = Mathf.Min(maxHp, _hp + amount);
            }

            public void ApplyStun(float duration)
            {
                if (IsDead || duration <= 0f)
                    return;

                _stunRemaining = Mathf.Max(_stunRemaining, duration);
            }
        }
    }
