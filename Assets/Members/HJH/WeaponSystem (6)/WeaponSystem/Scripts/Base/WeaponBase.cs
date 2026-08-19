using System;
using System.Collections.Generic;

namespace RobotWeapons
{
    public abstract class WeaponBase : IWeapon
    {
        public WeaponType Type { get; protected set; }
        public float CurrentResource { get; protected set; }
        public float MaxResource { get; protected set; }
        public bool IsReloading { get; protected set; }
        protected float reloadTimer;

        protected IWeaponOwner owner;
        protected WeaponData baseData;

        protected float bonusDamage;
        protected float bonusHeal;
        protected float bonusResource;

        private readonly List<UpgradeData> appliedUpgrades = new();

        public event Action<string> OnAttackTriggered;
        public event Action<float> OnDamageDealt;
        public event Action<float> OnHealApplied;

        protected WeaponBase(WeaponData data)
        {
            baseData = data;
            Type = data.type;
            MaxResource = data.resourceMax;
            CurrentResource = MaxResource;
        }

        public virtual void Equip(IWeaponOwner newOwner) => owner = newOwner;
        public virtual void Unequip() => owner = null;

        public virtual void RestoreUpgrades(IEnumerable<UpgradeData> upgrades)
        {
            foreach (var u in upgrades)
                ApplyUpgrade(u);
        }

        public virtual void ApplyUpgrade(UpgradeData upgrade)
        {
            if (upgrade == null) return;

            bonusDamage += upgrade.damageAdd;
            bonusHeal += upgrade.healAdd;
            bonusResource += upgrade.resourceAdd;
            MaxResource = baseData.resourceMax + bonusResource;

            appliedUpgrades.Add(upgrade);
        }

        public IReadOnlyList<UpgradeData> AppliedUpgrades => appliedUpgrades;

        public virtual bool PrimaryIsHeld => false;

        public virtual void Reload()
        {
            if (IsReloading || CurrentResource >= MaxResource) return;
            IsReloading = true;
            reloadTimer = baseData.reloadDuration;
        }

        protected void TickReload(float dt)
        {
            if (!IsReloading) return;
            reloadTimer -= dt;
            if (reloadTimer <= 0f)
            {
                CurrentResource = MaxResource;
                IsReloading = false;
            }
        }

        public abstract void PrimaryAttack();
        public virtual void SecondaryAction() { }
        public virtual void Tick(float dt) { }

        public virtual void ExecuteHit() { }
        public virtual void StartHitWindow() { }
        public virtual void EndHitWindow() { }
        public virtual void OnHitboxTouch(IDamageable target, bool isWeakpoint) { }

        protected void RaiseAttackTriggered(string animId) => OnAttackTriggered?.Invoke(animId);
        protected void RaiseDamage(float amount) => OnDamageDealt?.Invoke(amount);
        protected void RaiseHeal(float amount) => OnHealApplied?.Invoke(amount);
    }
}
