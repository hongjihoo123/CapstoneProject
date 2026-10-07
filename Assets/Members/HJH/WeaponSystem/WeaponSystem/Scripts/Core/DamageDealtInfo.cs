namespace RobotWeapons
{
    // Raised by the attacker after damage is applied (final amount, after stat multipliers).
    public readonly struct DamageDealtInfo
    {
        public readonly IDamageable Target;
        public readonly float Amount;
        public readonly bool IsWeakpoint;
        public readonly bool Killed;

        public DamageDealtInfo(IDamageable target, float amount, bool isWeakpoint, bool killed)
        {
            Target = target;
            Amount = amount;
            IsWeakpoint = isWeakpoint;
            Killed = killed;
        }
    }
}
