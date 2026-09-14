using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerHealth : AgentHealth, IAfterInitModule
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private float stunOnHitDuration;

        private PlayerStatsModule _stats;
        private float _hp;
        private float _stunRemaining;

        public override float CurrentHealth
        {
            get => _hp;
            set => _hp = Mathf.Clamp(value, 0f, MaxHp);
        }
        public float Hp => _hp;
        public float MaxHp => _stats != null && _stats.Tree != null ? _stats.Get(PlayerStatId.MaxHp) : maxHp;
        public bool IsDead => _hp <= 0f;
        public bool IsStunned => _stunRemaining > 0f;

            public override void Initialize(ModuleOwner owner)
            {
                base.Initialize(owner);
                InitHealth(maxHp);
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

            public override void TakeDamage(float amount)
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

        public void AfterInit()
        {
            _stats = _owner.GetModule<PlayerStatsModule>();
            InitHealth(MaxHp);
        }
    }
    }
