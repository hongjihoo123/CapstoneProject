namespace Members.JJH._02_Scripts.Agents.Modules
{
    public interface IHealth
    {
        public float CurrentHealth { get; }

        public void InitHealth(float maxHealth);
        public void TakeDamage(float damage);
    }
}