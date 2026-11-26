using UnityEngine.Events;

namespace Members.JJH._02_Scripts.Agents.Modules
{
    public interface IHealth
    {
        UnityEvent OnDeathEvent { get; }

        float CurrentHealth { get; }

        void InitHealth(float maxHealth);
        void TakeDamage(float damage);
    }
}