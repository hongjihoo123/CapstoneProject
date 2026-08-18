using UnityEngine;

namespace RobotWeapons
{
    public class HealerWeapon : WeaponBase
    {
        private enum ShotMode { Heal, Grenade }

        private readonly HealerData data;
        private ShotMode mode = ShotMode.Heal;

        public HealerWeapon(HealerData d) : base(d) { data = d; }

        public override void PrimaryAttack()
        {
            if (mode == ShotMode.Heal) FireHealShot();
            else ThrowGrenade();
        }

        public override void SecondaryAction()
        {
            mode = mode == ShotMode.Heal ? ShotMode.Grenade : ShotMode.Heal;
            RaiseAttackTriggered(mode == ShotMode.Heal ? "Healer_SwitchToHealShot" : "Healer_SwitchToGrenade");
        }

        private void FireHealShot()
        {
            if (owner == null || data.healShotPrefab == null) return;

            GameObject shot = GameObject.Instantiate(data.healShotPrefab, owner.AttackOrigin.position, owner.AttackOrigin.rotation);
            if (shot.TryGetComponent<HealShotProjectile>(out var hs))
                hs.Init(data.healShotAmount + bonusHeal, data.healShotSpeed, owner);

            RaiseAttackTriggered("Healer_Shot");
        }

        private void ThrowGrenade()
        {
            if (owner == null || data.grenadePrefab == null) return;

            GameObject g = GameObject.Instantiate(data.grenadePrefab, owner.AttackOrigin.position, Quaternion.identity);

            float rad = data.throwUpwardAngle * Mathf.Deg2Rad;
            Vector3 dir = (owner.AttackOrigin.forward * Mathf.Cos(rad) + Vector3.up * Mathf.Sin(rad)).normalized;

            if (g.TryGetComponent<Rigidbody>(out var rb))
                rb.linearVelocity = dir * data.throwForce;

            if (g.TryGetComponent<HealGrenade>(out var grenade))
                grenade.Init(data.grenadeDamage + bonusDamage, data.grenadeHeal + bonusHeal, data.aoeRadius, data.grenadeFuseTime, owner);

            RaiseAttackTriggered("Healer_ThrowGrenade");
        }
    }
}
