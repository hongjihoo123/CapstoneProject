using Members.JJH._02_Scripts.Systems.ModuleSystem;
using RobotWeapons;
using Unity.Cinemachine;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerWeapon : Module
    {
        [SerializeField] private WeaponData equippedWeaponData;
        [SerializeField] private WeaponHitbox weaponHitbox;
        [SerializeField] private bool treatSecondaryAsAim = true;

        [Header("È­¸é Èçµé¸²")]
        [SerializeField] private CinemachineImpulseSource impulseSource;
        [SerializeField] private float gunShakeForce = 0.3f;
        [SerializeField] private float energyBallShakeForce = 1f;

        [Header("¹ß»ç ÀÌÆåÆ®")]
        [SerializeField] private MuzzleFlash muzzleFlash;
        [SerializeField] private TracerVisual tracerVisual;

        [SerializeField] private float dutchSpringStrength = 400f;
        [SerializeField] private float dutchDamping = 4f;

        private IWeapon _weapon;
        private bool _fsmWantsAim;
        private WeaponData _lastEquippedData;
        private float _dutch;
        private float _dutchVelocity;
        private Quaternion _aimOriginBaseRotation;
        private bool _aimOriginBaseCaptured;

        public IWeapon Weapon => _weapon;
        public bool CanStartReload =>
            _weapon != null && !_weapon.IsReloading && _weapon.CurrentResource < _weapon.MaxResource;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _lastEquippedData = equippedWeaponData;
            if (equippedWeaponData == null)
                return;
            Equip(WeaponFactory.Create(equippedWeaponData));
        }

        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            if (equippedWeaponData == _lastEquippedData) return;

            _lastEquippedData = equippedWeaponData;
            if (equippedWeaponData != null)
                Equip(WeaponFactory.Create(equippedWeaponData));
        }

        private void OnDestroy()
        {
            if (_weapon != null)
                _weapon.OnAttackTriggered -= HandleAttackTriggered;
        }

        public void Equip(IWeapon weapon)
        {
            if (_weapon != null)
                _weapon.OnAttackTriggered -= HandleAttackTriggered;

            _weapon?.Unequip();
            _weapon = weapon;
            _fsmWantsAim = false;

            if (_weapon == null || _owner is not IWeaponOwner weaponOwner)
                return;

            _weapon.Equip(weaponOwner);
            _weapon.OnAttackTriggered += HandleAttackTriggered;
            weaponHitbox?.Init(_weapon);
        }

        public void Tick(float deltaTime)
        {
            _weapon?.Tick(deltaTime);
            UpdateDutchRoll(deltaTime);
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

        private void UpdateDutchRoll(float deltaTime)
        {
            if (_owner is not IWeaponOwner weaponOwner) return;
            Transform aimOrigin = weaponOwner.AimOrigin;
            if (aimOrigin == null) return;

            if (!_aimOriginBaseCaptured)
            {
                _aimOriginBaseRotation = aimOrigin.localRotation;
                _aimOriginBaseCaptured = true;
            }

            _dutchVelocity += -_dutch * dutchSpringStrength * deltaTime;
            _dutchVelocity *= Mathf.Clamp01(1f - dutchDamping * deltaTime);
            _dutch += _dutchVelocity * deltaTime;

            aimOrigin.localRotation = _aimOriginBaseRotation * Quaternion.Euler(0f, 0f, _dutch);
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

        public void ApplyRecoil(float pitchDelta, float yawDelta, float dutchImpulse = 0f)
        {
            if (_owner is Members.KYR._01_Scripts.PlayerAgent player && player.Mover != null)
            {
                player.Mover.ApplyRecoilPitch(pitchDelta);
                if (!Mathf.Approximately(yawDelta, 0f))
                    player.Mover.ApplyRecoilYaw(yawDelta);
            }
            _dutchVelocity += dutchImpulse;
        }

        private void HandleAttackTriggered(string animId)
        {
            switch (animId)
            {
                case "Gun_Fire":
                    muzzleFlash?.Play();
                    impulseSource?.GenerateImpulseWithForce(gunShakeForce);
                    break;
                case "Laser_EnergyBall":
                    float chargeRatio = (_weapon as LaserDealerWeapon)?.LastFireChargeRatio ?? 1f;
                    impulseSource?.GenerateImpulseWithForce(energyBallShakeForce * chargeRatio);
                    break;
                case "Sniper_Fire":
                    if (_weapon is SniperSawedOffWeapon sniperShot)
                        tracerVisual?.Fire(sniperShot.LastShotStart, sniperShot.LastShotEnd);
                    impulseSource?.GenerateImpulseWithForce(gunShakeForce);
                    break;
                case "SawedOff_FireLeft":
                case "SawedOff_FireRight":
                    muzzleFlash?.Play();
                    impulseSource?.GenerateImpulseWithForce(gunShakeForce);
                    break;
            }
        }
    }
}