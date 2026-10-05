using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerStatsModule : Module
    {
        [Header("Survival")]
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private float defense;
        [SerializeField] private float healReceived = 1f;

        [Header("Mobility")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7.5f;
        [SerializeField] private float dashSpeed = 1f;
        [SerializeField] private float dashDuration = 1f;

        [Header("Combat")]
        [SerializeField] private float damage = 1f;
        [SerializeField] private float attackSpeed = 1f;
        [SerializeField] private float weakpointMultiplier = 1f;

        [Header("Skill")]
        [SerializeField] private float skillCooldownReduction;

        public PlayerStatTree Tree { get; private set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            Tree = new PlayerStatTree(new PlayerStatBases
            {
                MaxHp = maxHp,
                Defense = defense,
                HealReceived = healReceived,
                WalkSpeed = walkSpeed,
                RunSpeed = runSpeed,
                DashSpeed = dashSpeed,
                DashDuration = dashDuration,
                Damage = damage,
                AttackSpeed = attackSpeed,
                WeakpointMultiplier = weakpointMultiplier,
                SkillCooldownReduction = skillCooldownReduction
            });
        }

        public float Get(PlayerStatId id)
        {
            return Tree.Get(id);
        }

        public void AddModifier(PlayerStatId id, StatModifier modifier)
        {
            Tree.AddModifier(id, modifier);
        }

        public void RemoveModifiers(object source)
        {
            Tree.RemoveModifiers(source);
        }

        public void Tick(float deltaTime)
        {
            Tree?.Tick(deltaTime);
        }

        [ContextMenu("Log Stats")]
        private void LogStats()
        {
            if (Tree == null)
            {
                Debug.Log($"{name} Stats tree is not initialized.", this);
                return;
            }

            Debug.Log(
                $"{name} MaxHp={Get(PlayerStatId.MaxHp):0.##} Def={Get(PlayerStatId.Defense):0.##} " +
                $"HealRecv={Get(PlayerStatId.HealReceived):0.##} " +
                $"Walk={Get(PlayerStatId.WalkSpeed):0.##} Run={Get(PlayerStatId.RunSpeed):0.##} " +
                $"Dash={Get(PlayerStatId.DashSpeed):0.##}/{Get(PlayerStatId.DashDuration):0.##} " +
                $"Dmg={Get(PlayerStatId.Damage):0.##} AtkSpd={Get(PlayerStatId.AttackSpeed):0.##} " +
                $"Weak={Get(PlayerStatId.WeakpointMultiplier):0.##} " +
                $"Cdr={Get(PlayerStatId.SkillCooldownReduction):0.##}",
                this);
        }
    }
}
