using System;
using System.Collections.Generic;

namespace Members.KYR._01.Scripts.Stats
{
    public sealed class PlayerStatTree
    {
        private readonly Dictionary<PlayerStatId, StatNode> _nodes = new();

        public StatNode Root { get; }

        public PlayerStatTree(PlayerStatBases bases)
        {
            Root = new StatNode(PlayerStatId.None, 0f);

            //----------공격----------
            Add(PlayerStatId.Damage, bases.Damage, Root);
            Add(PlayerStatId.AttackSpeed, bases.AttackSpeed, Root);
            Add(PlayerStatId.ArmorPenetration, bases.ArmorPenetration, Root);
            Add(PlayerStatId.MagicPenetration, bases.MagicPenetration, Root);
            Add(PlayerStatId.Omnivamp, bases.Omnivamp, Root);
            Add(PlayerStatId.SkillCooldownReduction, bases.SkillCooldownReduction, Root);
            Add(PlayerStatId.FirePenetration, bases.FirePenetration, Root);
            Add(PlayerStatId.WaterPenetration, bases.WaterPenetration, Root);
            Add(PlayerStatId.WindPenetration, bases.WindPenetration, Root);
            Add(PlayerStatId.ElectricPenetration, bases.ElectricPenetration, Root);
            Add(PlayerStatId.EarthPenetration, bases.EarthPenetration, Root);

            //----------방어----------
            Add(PlayerStatId.MaxHp, bases.MaxHp, Root);
            Add(PlayerStatId.DamageReduction, bases.DamageReduction, Root);
            Add(PlayerStatId.IncomingDamageTaken, bases.IncomingDamageTaken, Root);
            Add(PlayerStatId.HealReceived, bases.HealReceived, Root);
            Add(PlayerStatId.Tenacity, bases.Tenacity, Root);
            Add(PlayerStatId.ArmorDefense, bases.ArmorDefense, Root);
            Add(PlayerStatId.MagicDefense, bases.MagicDefense, Root);

            //----------이동----------
            Add(PlayerStatId.MoveSpeed, bases.MoveSpeed, Root);
            Add(PlayerStatId.DashSpeed, bases.DashSpeed, Root);
            Add(PlayerStatId.DashDuration, bases.DashDuration, Root);
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