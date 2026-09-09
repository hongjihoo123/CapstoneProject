namespace Members.KYR._01_Scripts.Stats
{
    public readonly struct StatModifier
    {
        public readonly object Source;
        public readonly StatModifierType Type;
        public readonly float Value;

        public StatModifier(object source, StatModifierType type, float value)
        {
            Source = source;
            Type = type;
            Value = value;
        }
    }
}
