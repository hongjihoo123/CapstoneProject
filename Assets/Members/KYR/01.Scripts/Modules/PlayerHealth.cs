using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts.Combat;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerHealth : Module, IHealth, IAfterInitModule
    {
        [SerializeField] private float stunOnHitDuration;
        [Header("스탯 풀백 모듈 없을 때")]
        [SerializeField] private float maxHp = 100f;

        private PlayerStatsModule _stats;
        private float _hp;
        private float _shield;
        private float _stunRemaining;

        public float CurrentHealth => _hp;
        public float CurrentShield => _shield;
        public float MaxHealth => MaxHp;
        public float MaxHp => _stats != null && _stats.Tree != null ? _stats.Get(PlayerStatId.MaxHp) : maxHp;
        public bool IsDead => _hp <= 0f;
        public bool IsStunned => _stunRemaining > 0f;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _stats = owner.GetModule<PlayerStatsModule>();
            _hp = maxHp;
            _shield = 0f;
            _stunRemaining = 0f;
        }

        public void AfterInitalize()
        {
            _hp = MaxHp;
            _shield = 0f;
        }

        public void InitHealth(float maxHealth)
        {
            maxHp = maxHealth;
            _hp = MaxHp;
            _shield = 0f;
        }

        public void Tick(float deltaTime)
        {
            ClampHpToMax();

            if (IsDead)
                _shield = 0f;
            else if (_shield > 0f)
                _shield = Mathf.Max(0f, _shield - Mathf.Max(0f, deltaTime));

            if (_stunRemaining <= 0f)
                return;

            _stunRemaining -= deltaTime;
            if (_stunRemaining < 0f)
                _stunRemaining = 0f;
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, null, null);
        }

        public void TakeDamage(float amount, ElementType? element)
        {
            TakeDamage(amount, null, element);
        }

        // 이미 공격 배율이 들어간 피해. 방어 계산만 하고 적용
        public void TakeDamage(float amount, GameObject source, ElementType? element)
        {
            if (IsDead || amount <= 0f)
                return;

            var info = new DamageInfo(amount, source, DamageCalculator.StatsOf(source), element: element);
            ApplyDamage(DamageCalculator.Incoming(amount, info, _stats));
        }

        // 방어 계산이 끝난 최종 피해만 넣을 것
        public DamageResult ApplyDamage(float damage)
        {
            ClampHpToMax();
            if (IsDead || damage <= 0f)
                return default;

            float absorbed = 0f;
            float remaining = damage;
            if (_shield > 0f)
            {
                absorbed = Mathf.Min(_shield, remaining);
                _shield -= absorbed;
                remaining -= absorbed;
            }

            float before = _hp;
            if (remaining > 0f)
                _hp = Mathf.Max(0f, _hp - remaining);

            if (IsDead)
                _shield = 0f;
            else if (remaining > 0f && stunOnHitDuration > 0f)
                ApplyStun(stunOnHitDuration);

            return new DamageResult(damage, absorbed, before - _hp, IsDead);
        }

        public void AddShield(float amount)
        {
            if (IsDead || amount <= 0f)
                return;

            _shield += amount;
        }

        public void ClearShield()
        {
            _shield = 0f;
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
