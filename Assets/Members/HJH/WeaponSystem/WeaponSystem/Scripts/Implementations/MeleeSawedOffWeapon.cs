using System.Collections.Generic;

namespace RobotWeapons
{
    public class MeleeSawedOffWeapon : WeaponBase
    {
        private enum Mode { Melee, Shotgun }

        private readonly MeleeSawedOffData data;
        private readonly SawedOffShotgunModule shotgun;
        private Mode mode = Mode.Melee;

        private int comboIndex;
        private bool hitWindowActive;
        private readonly HashSet<IDamageable> hitThisSwing = new();

        public bool IsShotgunMode => mode == Mode.Shotgun;
        public int ShotgunCurrentAmmo => shotgun.CurrentAmmo;
        public int ShotgunMaxAmmo => shotgun.MaxAmmo;
        public bool ShotgunIsReloading => shotgun.IsReloading;

        public MeleeSawedOffWeapon(MeleeSawedOffData d) : base(d)
        {
            data = d;
            shotgun = new SawedOffShotgunModule(d.pelletCount, d.spreadAngle, d.damagePerPellet,
                d.shotgunRange, d.shotgunMaxAmmo, d.shotgunReloadDuration, d.damageFalloffAtMaxRange,
                d.bulletPrefab, d.bulletSpeed);
        }

        public override void SwapMode()
        {
            mode = mode == Mode.Melee ? Mode.Shotgun : Mode.Melee;
            RaiseAttackTriggered(mode == Mode.Melee ? "Swap_Melee" : "Swap_Shotgun");
        }

        public override void PrimaryAttack()
        {
            if (mode == Mode.Melee) FireMelee();
            else shotgun.Fire(owner, bonusDamage, RaiseAttackTriggered, data.defaultHitEffectPrefab);
        }

        public override void Reload()
        {
            if (mode == Mode.Shotgun) shotgun.Reload();
        }

        public override void Tick(float dt) => shotgun.Tick(dt);

        private void FireMelee()
        {
            if (data.comboAnimIds == null || data.comboAnimIds.Length == 0) return;
            string animId = data.comboAnimIds[comboIndex];
            comboIndex = (comboIndex + 1) % data.comboAnimIds.Length;
            RaiseAttackTriggered(animId);
        }

        public override void StartHitWindow()
        {
            if (mode != Mode.Melee) return;
            hitThisSwing.Clear();
            hitWindowActive = true;
            owner?.SetWeaponHitboxActive(true);
        }

        public override void EndHitWindow()
        {
            hitWindowActive = false;
            owner?.SetWeaponHitboxActive(false);
        }

        public override void OnHitboxTouch(IDamageable target, bool isWeakpoint)
        {
            if (!hitWindowActive || target == null || !target.IsAlive || hitThisSwing.Contains(target)) return;
            hitThisSwing.Add(target);

            float dmg = data.meleeDamage + bonusDamage;
            owner.ApplyDamageTo(target, dmg, isWeakpoint);
            RaiseDamage(dmg);
        }
    }
}
