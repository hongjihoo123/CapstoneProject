using System;
using System.Collections.Generic;

namespace Members.KYR._01_Scripts.Stats
{
    public sealed class PlayerStatTree
    {
        private readonly Dictionary<PlayerStatId, StatNode> _nodes = new();

        public StatNode Root { get; }

        public PlayerStatTree(float maxHp, float walkSpeed, float runSpeed, float crouchSpeed, float damage)
        {
            Root = new StatNode(PlayerStatId.None, 0f);

            StatNode survival = Add(PlayerStatId.Survival, 0f, Root);
            Add(PlayerStatId.MaxHp, maxHp, survival);

            StatNode mobility = Add(PlayerStatId.Mobility, 0f, Root);
            Add(PlayerStatId.WalkSpeed, walkSpeed, mobility);
            Add(PlayerStatId.RunSpeed, runSpeed, mobility);
            Add(PlayerStatId.CrouchSpeed, crouchSpeed, mobility);

            StatNode combat = Add(PlayerStatId.Combat, 0f, Root);
            Add(PlayerStatId.Damage, damage, combat);
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
