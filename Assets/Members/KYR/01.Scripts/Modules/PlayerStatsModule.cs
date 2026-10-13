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

        [Header("Skill")]
        [SerializeField] private float skillCooldownReduction;

        [Header("Element Attack")]
        [SerializeField] private float fireDamage = 1f;
        [SerializeField] private float waterDamage = 1f;
        [SerializeField] private float windDamage = 1f;
        [SerializeField] private float electricDamage = 1f;
        [SerializeField] private float earthDamage = 1f;

        [Header("Element Defense")]
        [SerializeField] private float fireDefense;
        [SerializeField] private float waterDefense;
        [SerializeField] private float windDefense;
        [SerializeField] private float electricDefense;
        [SerializeField] private float earthDefense;

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
                SkillCooldownReduction = skillCooldownReduction,
                FireDamage = fireDamage,
                WaterDamage = waterDamage,
                WindDamage = windDamage,
                ElectricDamage = electricDamage,
                EarthDamage = earthDamage,
                FireDefense = fireDefense,
                WaterDefense = waterDefense,
                WindDefense = windDefense,
                ElectricDefense = electricDefense,
                EarthDefense = earthDefense
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
                $"Cdr={Get(PlayerStatId.SkillCooldownReduction):0.##} " +
                $"FireAtk={Get(PlayerStatId.FireDamage):0.##} WaterAtk={Get(PlayerStatId.WaterDamage):0.##} " +
                $"WindAtk={Get(PlayerStatId.WindDamage):0.##} ElecAtk={Get(PlayerStatId.ElectricDamage):0.##} " +
                $"EarthAtk={Get(PlayerStatId.EarthDamage):0.##} " +
                $"FireDef={Get(PlayerStatId.FireDefense):0.##} WaterDef={Get(PlayerStatId.WaterDefense):0.##} " +
                $"WindDef={Get(PlayerStatId.WindDefense):0.##} ElecDef={Get(PlayerStatId.ElectricDefense):0.##} " +
                $"EarthDef={Get(PlayerStatId.EarthDefense):0.##}",
                this);
        }
    }
}
