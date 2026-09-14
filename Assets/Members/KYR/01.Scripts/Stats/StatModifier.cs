namespace Members.KYR._01_Scripts.Stats
{
    public readonly struct StatModifier
    {
        public readonly object Source;
        public readonly StatModifierType Type;
        public readonly float Value;
        public readonly float Duration;

        public StatModifier(object source, StatModifierType type, float value)
            : this(source, type, value, 0f)
        {
        }

        public StatModifier(object source, StatModifierType type, float value, float duration)
        {
            Source = source;
            Type = type;
            Value = value;
            Duration = duration;
        }

        public bool IsTimed => Duration > 0f;
    }
}
