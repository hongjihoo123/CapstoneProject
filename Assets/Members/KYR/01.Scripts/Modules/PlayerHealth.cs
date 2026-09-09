using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerHealth : Module, IHealth, IAfterInitModule
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private float stunOnHitDuration;

        private PlayerStatsModule _stats;
        private float _hp;
        private float _stunRemaining;

        public float CurrentHealth => _hp;
        public float Hp => _hp;
        public float MaxHp => _stats != null && _stats.Tree != null ? _stats.Get(PlayerStatId.MaxHp) : maxHp;
        public bool IsDead => _hp <= 0f;
        public bool IsStunned => _stunRemaining > 0f;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _stats = owner.GetModule<PlayerStatsModule>();
            _hp = maxHp;
            _stunRemaining = 0f;
        }

        public void AfterInit()
        {
            _hp = MaxHp;
        }

        public void InitHealth(float maxHealth)
        {
            maxHp = maxHealth;
            _hp = MaxHp;
        }

        public void Tick(float deltaTime)
        {
            ClampHpToMax();

            if (_stunRemaining <= 0f)
                return;

            _stunRemaining -= deltaTime;
            if (_stunRemaining < 0f)
                _stunRemaining = 0f;
        }

        public void TakeDamage(float amount)
        {
            ClampHpToMax();
            if (IsDead || amount <= 0f)
                return;

            float defense = _stats != null && _stats.Tree != null
                ? Mathf.Clamp01(_stats.Get(PlayerStatId.Defense))
                : 0f;
            amount *= 1f - defense;

            _hp = Mathf.Max(0f, _hp - amount);
            if (!IsDead && stunOnHitDuration > 0f)
                ApplyStun(stunOnHitDuration);
        }

        public void Heal(float amount)
        {
            ClampHpToMax();
            if (IsDead || amount <= 0f)
                return;

            float received = _stats != null && _stats.Tree != null
                ? _stats.Get(PlayerStatId.HealReceived)
                : 1f;
            _hp = Mathf.Min(MaxHp, _hp + amount * received);
        }

        public void ApplyStun(float duration)
        {
            if (IsDead || duration <= 0f)
                return;

            _stunRemaining = Mathf.Max(_stunRemaining, duration);
        }

        private void ClampHpToMax()
        {
            float max = Mathf.Max(0f, MaxHp);
            if (_hp > max)
                _hp = max;
        }
    }
}
