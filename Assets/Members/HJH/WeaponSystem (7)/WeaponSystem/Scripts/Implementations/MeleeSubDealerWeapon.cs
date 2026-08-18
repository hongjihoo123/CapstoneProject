using System.Collections.Generic;

namespace RobotWeapons
{
    public class MeleeSubDealerWeapon : WeaponBase
    {
        private readonly MeleeSubDealerData data;
        private int comboIndex;
        private bool hitWindowActive;
        private readonly HashSet<IDamageable> hitThisSwing = new();

        public MeleeSubDealerWeapon(MeleeSubDealerData d) : base(d) { data = d; }

        public override void PrimaryAttack()
        {
            if (data.comboAnimIds == null || data.comboAnimIds.Length == 0) return;

            string animId = data.comboAnimIds[comboIndex];
            comboIndex = (comboIndex + 1) % data.comboAnimIds.Length;
            RaiseAttackTriggered(animId);
        }

        public override void StartHitWindow()
        {
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

            float dmg = data.damage + bonusDamage;
            owner.ApplyDamageTo(target, dmg);
            RaiseDamage(dmg);
        }
    }
}
