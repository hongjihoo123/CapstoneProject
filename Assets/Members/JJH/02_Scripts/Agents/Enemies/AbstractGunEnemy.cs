using RobotWeapons;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public class AbstractGunEnemy : AbstractEnemy, IWeaponOwner
    {
        [Header("Gun Data")]
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform muzzleOrigin;
        [SerializeField] private WeaponData equippedWeaponData;
        [SerializeField] private WeaponHitbox weaponHitbox;

        public IWeapon Weapon { get; private set; }

        public Transform AimOrigin => aimOrigin != null ? aimOrigin : transform;
        public Transform MuzzleOrigin => muzzleOrigin != null ? muzzleOrigin : transform;

        protected override void InitializeModules()
        {
            base.InitializeModules();


            if (equippedWeaponData != null)
            {
                Weapon = WeaponFactory.Create(equippedWeaponData);
                Weapon.Equip(this);
                weaponHitbox?.Init(Weapon);
            }
        }

        private void Update()
        {
            Weapon?.Tick(Time.deltaTime);
        }

        public override void Attack()
        {
            base.Attack();

            Weapon?.PrimaryAttack();
        }

        public void SetWeaponHitboxActive(bool active)
            => weaponHitbox?.SetActive(active);
    }
}