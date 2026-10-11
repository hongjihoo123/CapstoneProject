using System;
using System.Collections.Generic;

namespace Members.KYR._01_Scripts.Stats
{
    public sealed class PlayerStatTree
    {
        private readonly Dictionary<PlayerStatId, StatNode> _nodes = new();

        public StatNode Root { get; }

        public PlayerStatTree(PlayerStatBases bases)
        {
            Root = new StatNode(PlayerStatId.None, 0f);

            StatNode survival = Add(PlayerStatId.Survival, 0f, Root);
            Add(PlayerStatId.MaxHp, bases.MaxHp, survival);
            Add(PlayerStatId.Defense, bases.Defense, survival);
            Add(PlayerStatId.HealReceived, bases.HealReceived, survival);
            Add(PlayerStatId.FireDefense, bases.FireDefense, survival);
            Add(PlayerStatId.WaterDefense, bases.WaterDefense, survival);
            Add(PlayerStatId.WindDefense, bases.WindDefense, survival);
            Add(PlayerStatId.ElectricDefense, bases.ElectricDefense, survival);
            Add(PlayerStatId.EarthDefense, bases.EarthDefense, survival);

            StatNode mobility = Add(PlayerStatId.Mobility, 0f, Root);
            Add(PlayerStatId.WalkSpeed, bases.WalkSpeed, mobility);
            Add(PlayerStatId.RunSpeed, bases.RunSpeed, mobility);
            Add(PlayerStatId.DashSpeed, bases.DashSpeed, mobility);
            Add(PlayerStatId.DashDuration, bases.DashDuration, mobility);

            StatNode combat = Add(PlayerStatId.Combat, 0f, Root);
            Add(PlayerStatId.Damage, bases.Damage, combat);
            Add(PlayerStatId.AttackSpeed, bases.AttackSpeed, combat);
            Add(PlayerStatId.WeakpointMultiplier, bases.WeakpointMultiplier, combat);

            StatNode skill = Add(PlayerStatId.Skill, 0f, Root);
            Add(PlayerStatId.SkillCooldownReduction, bases.SkillCooldownReduction, skill);

            StatNode element = Add(PlayerStatId.Element, 0f, Root);
            Add(PlayerStatId.FireDamage, bases.FireDamage, element);
            Add(PlayerStatId.WaterDamage, bases.WaterDamage, element);
            Add(PlayerStatId.WindDamage, bases.WindDamage, element);
            Add(PlayerStatId.ElectricDamage, bases.ElectricDamage, element);
            Add(PlayerStatId.EarthDamage, bases.EarthDamage, element);
        }

        public float Get(PlayerStatId id)
        {
            return GetNode(id).ComputeFinal();
        }

        public void AddModifier(PlayerStatId id, StatModifier modifier)
        {
            GetNode(id).AddModifier(modifier);
        }

        public void RemoveModifiers(object source)
        {
            Root.RemoveModifiers(source);
            foreach (StatNode node in _nodes.Values)
                node.RemoveModifiers(source);
        }

        public void Tick(float deltaTime)
        {
            Root.Tick(deltaTime);
            foreach (StatNode node in _nodes.Values)
                node.Tick(deltaTime);
        }

        public StatNode GetNode(PlayerStatId id)
        {
            if (!_nodes.TryGetValue(id, out StatNode node))
                throw new ArgumentException($"Unknown stat {id}.", nameof(id));

            return node;
        }

        private StatNode Add(PlayerStatId id, float baseValue, StatNode parent)
        {
            var node = new StatNode(id, baseValue);
            parent.AddChild(node);
            _nodes.Add(id, node);
            return node;
        }
    }
}
