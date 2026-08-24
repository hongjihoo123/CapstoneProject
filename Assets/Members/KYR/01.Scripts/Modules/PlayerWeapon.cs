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

        [Header("화면 흔들림")]
        [SerializeField] private CinemachineImpulseSource impulseSource;
        [SerializeField] private float gunShakeForce = 0.3f;
        [SerializeField] private float energyBallShakeForce = 1f;

        [Header("발사 이펙트 (선택)")]
        [SerializeField] private MuzzleFlash muzzleFlash;
        [SerializeField] private TracerVisual tracerVisual;

        [Header("리코일")]
        [SerializeField] private float dutchSpringStrength = 400f;
        [SerializeField] private float dutchDamping = 4f;
        [SerializeField, Range(0f, 1f)] private float crouchRecoilMultiplier = 0.5f;

        [Header("조준")]
        [SerializeField] private Vector3 adsCameraLocalOffset = new Vector3(0f, 0f, 0.15f);
        [SerializeField] private float aimFov = 50f;
        [SerializeField] private float aimFovTransitionSpeed = 14f;

        private bool _isAiming;
        private float _currentFov;
        private float _hipFov;
        private bool _fovInitialized;

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
            UpdateAimFov(deltaTime);
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
            _isAiming = wantAim;

            if (_owner is Members.KYR._01_Scripts.PlayerAgent player && player.Mover != null)
            {
                if (wantAim)
                    player.Mover.SetCameraLocalPositionInstant(player.Mover.HipCameraLocalPosition + adsCameraLocalOffset);
                else
                    player.Mover.ResetCameraToHipInstant();
            }

            if (treatSecondaryAsAim)
                _weapon?.SecondaryAction();
        }

        private void UpdateAimFov(float deltaTime)
        {
            if (_owner is not Members.KYR._01_Scripts.PlayerAgent player || player.CinemachineCamera == null)
                return;

            if (!_fovInitialized)
            {
                _hipFov = player.CinemachineCamera.Lens.FieldOfView;
                _currentFov = _hipFov;
                _fovInitialized = true;
            }

            float targetFov = _isAiming ? aimFov : _hipFov;
            _currentFov = Mathf.Lerp(_currentFov, targetFov, deltaTime * aimFovTransitionSpeed);
            player.CinemachineCamera.Lens.FieldOfView = _currentFov;
        }

        public void SetHitboxActive(bool active)
        {
            weaponHitbox?.SetActive(active);
        }

        public void ApplyRecoil(float pitchDelta, float yawDelta, float dutchImpulse = 0f)
        {
            if (_owner is Members.KYR._01_Scripts.PlayerAgent player && player.Mover != null)
            {
                float multiplier = (player.MoveFsm != null && player.MoveFsm.Capabilities.IsCrouching)
                    ? crouchRecoilMultiplier
                    : 1f;

                player.Mover.ApplyRecoilPitch(pitchDelta * multiplier);
                if (!Mathf.Approximately(yawDelta, 0f))
                    player.Mover.ApplyRecoilYaw(yawDelta * multiplier);

                dutchImpulse *= multiplier;
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