using Members.JJH._02_Scripts.Systems.ModuleSystem;
using UnityEngine;
namespace Members.JJH._02_Scripts.Agents.Modules
{
    public class AgentHealth : Module, IHealth
    {
        public virtual float CurrentHealth
        {
            get => _health;
            set
            {
                if (value <= 0)
                    _health = 0;
                else
                    _health = Mathf.Min(value, _maxHealth);
            }
        }
        private float _health;
        private float _maxHealth;
        public float MaxHealth => _maxHealth;
        public virtual void InitHealth(float maxHealth)
        {
            _maxHealth = maxHealth;
            CurrentHealth = _maxHealth;
        }
        public virtual void TakeDamage(float damage)
            => CurrentHealth -= damage;
    }
}
