using System;
using System.Collections.Generic;

namespace Members.KYR._01_Scripts.Stats
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
        public IReadOnlyList<StatNode> Children => _children;

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

            for (StatNode node = this; node != null; node = node.Parent)
                Accumulate(node, ref flat, ref percentAdd);

            return (BaseValue + flat) * (1f + percentAdd);
        }

        private static void Accumulate(StatNode node, ref float flat, ref float percentAdd)
        {
            for (int i = 0; i < node._modifiers.Count; i++)
            {
                StatModifier modifier = node._modifiers[i].Modifier;
                if (modifier.Type == StatModifierType.Flat)
                    flat += modifier.Value;
                else
                    percentAdd += modifier.Value;
            }
        }
    }
}
