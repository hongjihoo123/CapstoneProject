using Members.JJH._02_Scripts.Systems.ModuleSystem;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public class AgentHealth : Module, IHealth
    {
        public float CurrentHealth
        {
            get => _health;
            set
            {
                if (_health <= 0)
                    _health = 0;
                else
                    _health = Mathf.Min(value, _maxHealth);
            }
        }
        private float _health;

        private float _maxHealth;

        public void InitHealth(float maxHealth)
        {
            _maxHealth = maxHealth;
            CurrentHealth = _maxHealth;
        }

        public void TakeDamage(float damage)
            => CurrentHealth -= damage;
    }
}