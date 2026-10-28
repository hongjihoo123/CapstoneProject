using System;
using System.Collections.Generic;
using Members.KYR._01_Scripts.Stats;

namespace Members.KYR._01.Scripts.Stats
{
    public sealed class StatNode
    {
        private struct TimedModifier
        {
            public StatModifier Modifier;
            public float Remaining;
        }

        private readonly List<StatNode> _children = new();
        private readonly List<TimedModifier> _modifiers = new();

        public PlayerStatId Id { get; }
        public float BaseValue { get; set; }
        public StatNode Parent { get; private set; }

        public StatNode(PlayerStatId id, float baseValue)
        {
            Id = id;
            BaseValue = baseValue;
        }

        public void AddChild(StatNode child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            if (child.Parent != null)
                throw new InvalidOperationException($"{child.Id} already has a parent.");

            child.Parent = this;
            _children.Add(child);
        }

        public void AddModifier(StatModifier modifier)
        {
            _modifiers.Add(new TimedModifier
            {
                Modifier = modifier,
                Remaining = modifier.IsTimed ? modifier.Duration : -1f
            });
        }

        public void RemoveModifiers(object source)
        {
            _modifiers.RemoveAll(slot => Equals(slot.Modifier.Source, source));
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                TimedModifier slot = _modifiers[i];
                if (slot.Remaining < 0f)
                    continue;

                slot.Remaining -= deltaTime;
                if (slot.Remaining <= 0f)
                    _modifiers.RemoveAt(i);
                else
                    _modifiers[i] = slot;
            }
        }

        public float ComputeFinal()
        {
            float flat = 0f;
            float percentAdd = 0f;
            float percentMore = 1f;

            for (StatNode node = this; node != null; node = node.Parent)
                Accumulate(node, ref flat, ref percentAdd, ref percentMore);

            return (BaseValue + flat) * (1f + percentAdd) * percentMore;
        }

        private static void Accumulate(StatNode node, ref float flat, ref float percentAdd, ref float percentMore)
        {
            for (int i = 0; i < node._modifiers.Count; i++)
            {
                StatModifier modifier = node._modifiers[i].Modifier;
                switch (modifier.Type)
                {
                    case StatModifierType.Flat:
                        flat += modifier.Value;
                        break;
                    case StatModifierType.PercentAdd:
                        percentAdd += modifier.Value;
                        break;
                    case StatModifierType.PercentMore:
                        percentMore *= 1f + modifier.Value;
                        break;
                }
            }
        }
    }
}
