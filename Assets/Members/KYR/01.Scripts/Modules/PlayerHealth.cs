using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.ElementsSystem;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
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

        public void TakeDamage(float amount, GameObject source, ElementType? element)
        {
            ClampHpToMax();
            if (IsDead || amount <= 0f)
                return;

            float taken = amount;
            if (_stats != null && _stats.Tree != null)
            {
                PlayerStatsModule attacker = StatsFrom(source);
                bool hasElement = element.HasValue;
                float eleDef = 0f;
                float elePen = 0f;
                if (hasElement)
                {
                    eleDef = _stats.Get(PlayerElementStats.DefenseStat(element.Value));
                    if (attacker != null && attacker.Tree != null)
                        elePen = attacker.Get(PlayerElementStats.PenetrationStat(element.Value));
                }

                float armorPen = attacker != null && attacker.Tree != null
                    ? attacker.Get(PlayerStatId.ArmorPenetration)
                    : 0f;

                taken = StatFormulas.Incoming(
                    amount,
                    _stats.Get(PlayerStatId.Defense),
                    armorPen,
                    eleDef,
                    elePen,
                    _stats.Get(PlayerStatId.DamageReduction),
                    hasElement);
            }

            if (taken <= 0f)
                return;

            if (_shield > 0f)
            {
                float absorbed = Mathf.Min(_shield, taken);
                _shield -= absorbed;
                taken -= absorbed;
                if (taken <= 0f)
                    return;
            }

            _hp = Mathf.Max(0f, _hp - taken);
            if (IsDead)
            {
                _shield = 0f;
                return;
            }

            if (stunOnHitDuration > 0f)
                ApplyStun(stunOnHitDuration);
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

        private static PlayerStatsModule StatsFrom(GameObject source)
        {
            if (source == null)
                return null;
            if (source.TryGetComponent(out PlayerStatsModule stats))
                return stats;
            return source.GetComponentInParent<PlayerStatsModule>();
        }

        private void ClampHpToMax()
        {
            float max = Mathf.Max(0f, MaxHp);
            if (_hp > max)
                _hp = max;
        }
    }
}
