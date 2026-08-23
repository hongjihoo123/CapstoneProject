using Members.JJH._02_Scripts.Systems.ModuleSystem;
using RobotWeapons;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerWeapon : Module
    {
        [SerializeField] private WeaponData equippedWeaponData;
        [SerializeField] private WeaponHitbox weaponHitbox;
        [SerializeField] private bool treatSecondaryAsAim = true;

        private IWeapon _weapon;
        private bool _fsmWantsAim;

        public IWeapon Weapon => _weapon;

        public bool CanStartReload =>
            _weapon != null && !_weapon.IsReloading && _weapon.CurrentResource < _weapon.MaxResource;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            if (equippedWeaponData == null)
                return;

            Equip(WeaponFactory.Create(equippedWeaponData));
        }

        public void Equip(IWeapon weapon)
        {
            _weapon?.Unequip();
            _weapon = weapon;
            _fsmWantsAim = false;

            if (_weapon == null || _owner is not IWeaponOwner weaponOwner)
                return;

            _weapon.Equip(weaponOwner);
            weaponHitbox?.Init(_weapon);
        }

        public void Tick(float deltaTime)
        {
            _weapon?.Tick(deltaTime);
        }

        public void TryFire(bool fireHeld, bool firePressed)
        {
            if (_weapon == null)
                return;

            if (_weapon.PrimaryIsHeld)
            {
                if (fireHeld)
                    _weapon.PrimaryAttack();
                return;
            }

            if (firePressed)
                _weapon.PrimaryAttack();
        }

        public void RequestReload()
        {
            _weapon?.Reload();
        }

        public void SetAiming(bool wantAim)
        {
            if (_fsmWantsAim == wantAim)
                return;

            _fsmWantsAim = wantAim;
            if (treatSecondaryAsAim)
                _weapon?.SecondaryAction();
        }

        public void SetHitboxActive(bool active)
        {
            weaponHitbox?.SetActive(active);
        }
    }
}
