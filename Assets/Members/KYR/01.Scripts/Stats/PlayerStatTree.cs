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
            Add(PlayerStatId.MaxStamina, bases.MaxStamina, survival);
            Add(PlayerStatId.StaminaRegen, bases.StaminaRegen, survival);
            Add(PlayerStatId.HealReceived, bases.HealReceived, survival);

            StatNode mobility = Add(PlayerStatId.Mobility, 0f, Root);
            Add(PlayerStatId.WalkSpeed, bases.WalkSpeed, mobility);
            Add(PlayerStatId.RunSpeed, bases.RunSpeed, mobility);
            Add(PlayerStatId.CrouchSpeed, bases.CrouchSpeed, mobility);
            Add(PlayerStatId.JumpHeight, bases.JumpHeight, mobility);
            Add(PlayerStatId.DashSpeed, bases.DashSpeed, mobility);
            Add(PlayerStatId.DashDuration, bases.DashDuration, mobility);
            Add(PlayerStatId.AirControl, bases.AirControl, mobility);

            StatNode combat = Add(PlayerStatId.Combat, 0f, Root);
            Add(PlayerStatId.Damage, bases.Damage, combat);
            Add(PlayerStatId.AttackSpeed, bases.AttackSpeed, combat);
            Add(PlayerStatId.ReloadSpeed, bases.ReloadSpeed, combat);
            Add(PlayerStatId.RecoilControl, bases.RecoilControl, combat);
            Add(PlayerStatId.WeakpointMultiplier, bases.WeakpointMultiplier, combat);

            StatNode skill = Add(PlayerStatId.Skill, 0f, Root);
            Add(PlayerStatId.SkillCooldownReduction, bases.SkillCooldownReduction, skill);
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
