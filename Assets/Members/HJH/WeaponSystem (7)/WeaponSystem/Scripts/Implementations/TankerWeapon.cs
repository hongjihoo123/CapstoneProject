using System.Collections.Generic;
using UnityEngine;

namespace RobotWeapons
{
    public class TankerWeapon : WeaponBase
    {
        private readonly TankerWeaponData data;
        private bool hitWindowActive;
        private readonly HashSet<IDamageable> hitThisSwing = new();

        public TankerWeapon(TankerWeaponData d) : base(d) { data = d; }

        public override void PrimaryAttack()
        {
            if (data.mode == TankerWeaponData.Mode.RangedOnly) return;
            RaiseAttackTriggered(data.meleeSwingAnimId);
        }

        public override void StartHitWindow()
        {
            if (data.mode == TankerWeaponData.Mode.RangedOnly) return;
            hitThisSwing.Clear();
            hitWindowActive = true;
            owner?.SetWeaponHitboxActive(true);
        }

        public override void EndHitWindow()
        {
            hitWindowActive = false;
            owner?.SetWeaponHitboxActive(false);
        }

        public override void OnHitboxTouch(IDamageable target)
        {
            if (!hitWindowActive || target == null || !target.IsAlive || hitThisSwing.Contains(target)) return;
            hitThisSwing.Add(target);

            float dmg = data.meleeDamage + bonusDamage;
            owner.ApplyDamageTo(target, dmg);
            RaiseDamage(dmg);
        }

        public override void SecondaryAction()
        {
            if (data.mode == TankerWeaponData.Mode.MeleeOnly) return;
            if (owner == null || data.projectilePrefab == null) return;

            GameObject proj = GameObject.Instantiate(data.projectilePrefab, owner.AttackOrigin.position, owner.AttackOrigin.rotation);
            if (proj.TryGetComponent<Projectile>(out var p))
                p.Init(data.rangedDamagePerShot + bonusDamage, data.projectileSpeed, owner);

            RaiseAttackTriggered("Tanker_RangedShot");
        }
    }
}
