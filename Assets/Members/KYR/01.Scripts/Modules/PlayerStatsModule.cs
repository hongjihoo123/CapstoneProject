using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerStatsModule : Module
    {
        [Header("Formula")]
        [SerializeField] private CombatFormulaManagerSO combatFormula;

        [Header("Survival")]
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private float defense;
        [SerializeField] private float damageReduction;
        [SerializeField] private float maxStamina;
        [SerializeField] private float staminaRegen;
        [SerializeField] private float healReceived = 1f;
        [SerializeField] private float tenacity;
        [SerializeField] private float unflinching;

        [Header("Mobility")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7.5f;
        [SerializeField] private float crouchSpeed = 2.2f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float dashSpeed = 1f;
        [SerializeField] private float dashDuration = 1f;
        [SerializeField] private float airControl = 0.7f;

        [Header("Combat")]
        [SerializeField] private float damage = 1f;
        [SerializeField] private float attackSpeed = 1f;
        [SerializeField] private float reloadSpeed = 1f;
        [SerializeField] private float recoilControl = 1f;
        [SerializeField] private float weakpointMultiplier = 1f;
        [SerializeField] private float armorPenetration;
        [SerializeField] private float omnivamp;

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

        [Header("Element Penetration")]
        [SerializeField] private float firePenetration;
        [SerializeField] private float waterPenetration;
        [SerializeField] private float windPenetration;
        [SerializeField] private float electricPenetration;
        [SerializeField] private float earthPenetration;

        public PlayerStatTree Tree { get; private set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            if (combatFormula != null)
                StatFormulas.Settings = combatFormula;

            Tree = new PlayerStatTree(new PlayerStatBases
            {
                MaxHp = maxHp,
                Defense = defense,
                DamageReduction = damageReduction,
                MaxStamina = maxStamina,
                StaminaRegen = staminaRegen,
                HealReceived = healReceived,
                Tenacity = tenacity,
                Unflinching = unflinching,
                WalkSpeed = walkSpeed,
                RunSpeed = runSpeed,
                CrouchSpeed = crouchSpeed,
                JumpHeight = jumpHeight,
                DashSpeed = dashSpeed,
                DashDuration = dashDuration,
                AirControl = airControl,
                Damage = damage,
                AttackSpeed = attackSpeed,
                ReloadSpeed = reloadSpeed,
                RecoilControl = recoilControl,
                WeakpointMultiplier = weakpointMultiplier,
                ArmorPenetration = armorPenetration,
                Omnivamp = omnivamp,
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
                EarthDefense = earthDefense,
                FirePenetration = firePenetration,
                WaterPenetration = waterPenetration,
                WindPenetration = windPenetration,
                ElectricPenetration = electricPenetration,
                EarthPenetration = earthPenetration
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
                $"DR={Get(PlayerStatId.DamageReduction):0.##} " +
                $"HealRecv={Get(PlayerStatId.HealReceived):0.##} " +
                $"Walk={Get(PlayerStatId.WalkSpeed):0.##} Run={Get(PlayerStatId.RunSpeed):0.##} " +
                $"Crouch={Get(PlayerStatId.CrouchSpeed):0.##} " +
                $"Dash={Get(PlayerStatId.DashSpeed):0.##}/{Get(PlayerStatId.DashDuration):0.##} " +
                $"Dmg={Get(PlayerStatId.Damage):0.##} AtkSpd={Get(PlayerStatId.AttackSpeed):0.##} " +
                $"Reload={Get(PlayerStatId.ReloadSpeed):0.##} Pen={Get(PlayerStatId.ArmorPenetration):0.##} " +
                $"Omni={Get(PlayerStatId.Omnivamp):0.##} Cdr={Get(PlayerStatId.SkillCooldownReduction):0.##} " +
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
