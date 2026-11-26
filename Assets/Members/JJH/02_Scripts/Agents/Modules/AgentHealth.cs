
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using UnityEngine;
using UnityEngine.Events;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public class AgentHealth : AbstractModule, IHealth
    {
        [field: SerializeField] public UnityEvent OnDeathEvent { get; private set; }

        public virtual float CurrentHealth
        {
            get => _health;
            set
            {
                if (value <= 0)
                {
                    _health = 0;
                    OnDeathEvent?.Invoke();
                }
                else
                    _health = Mathf.Min(value, _maxHealth);
            }
        }

        public virtual float MaxHealth => _maxHealth;
        public virtual bool IsDead => _health <= 0f;

        protected float _health;
        protected float _maxHealth;

        public virtual void InitHealth(float maxHealth)
        {
            _maxHealth = maxHealth;
            CurrentHealth = _maxHealth;
        }

        public virtual void TakeDamage(float damage)
            => CurrentHealth -= damage;
    }
}
