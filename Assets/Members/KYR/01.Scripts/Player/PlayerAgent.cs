using Members.JJH._02_Scripts.Agents;
using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using Members.KYR._01_Scripts.FSM.Control;
using Members.KYR._01_Scripts.FSM.Move;
using Members.KYR._01_Scripts.FSM.Weapon;
using Members.KYR._01_Scripts.Modules;
using RobotWeapons;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01_Scripts
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerAgent : Agent, IWeaponOwner, IDamageable, IHealable
    {

        [SerializeField] private PlayerInputSO playerInput;

        [Header("총 관련")]
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform muzzleOrigin;
        [SerializeField] private Text ammoText;
        [Header("반동 관련")]
        [SerializeField] private CinemachineCamera cinemachineCamera;
        private float currentFOV;

        [SerializeField] private bool lockCursor = true;

        [Header("Animator")]
        [SerializeField] private AnimParamSO idleParam;
        [SerializeField] private AnimParamSO speedParam;
        [SerializeField] private AnimParamSO rand_Reload;
        [SerializeField] private AnimParamSO groundedParam;
        [SerializeField] private AnimParamSO crouchParam;
        [SerializeField] private AnimParamSO airborneParam;
        [SerializeField] private AnimParamSO isAimParam;
        [SerializeField] private AnimParamSO aimIdleParam;
        [SerializeField] private AnimParamSO isFireParam;
        [SerializeField] private AnimParamSO isAimFireParam;
        [SerializeField] private AnimParamSO reloadParam;

        [SerializeField] private float aimEnterPulseDuration = 0.15f;
        [SerializeField] private float firePulseDuration = 0.15f;

        private bool wasAimingLastFrame;
        private float aimEnterPulseTimer;
        private float firePulseTimer;
        private bool lastFireWasAimed;
        private bool wasReloadingLastFrame;


        public PlayerInputState Input { get; } = new();
        public PlayerMover Mover { get; private set; }
        public PlayerHealth Health { get; private set; }
        public PlayerWeapon Weapon { get; private set; }
        public ControlStateModule ControlFsm { get; private set; }
        public MoveStateModule MoveFsm { get; private set; }
        public WeaponStateModule WeaponFsm { get; private set; }

        public Transform AimOrigin => aimOrigin != null ? aimOrigin : transform;
        public Transform MuzzleOrigin => muzzleOrigin != null ? muzzleOrigin : transform;
        public bool IsAlive => Health != null && !Health.IsDead;
        public CinemachineCamera CinemachineCamera => cinemachineCamera;

        protected override void InitializeModules()
        {
            base.InitializeModules();

            AimUtility.IgnoreLayerMask = LayerMask.GetMask("Player");

            Mover = GetModule<PlayerMover>();
            Health = GetModule<PlayerHealth>();
            Weapon = GetModule<PlayerWeapon>();
            ControlFsm = GetModule<ControlStateModule>();
            MoveFsm = GetModule<MoveStateModule>();
            WeaponFsm = GetModule<WeaponStateModule>();

            Debug.Assert(playerInput != null, $"{name}에는 PlayerInputSO가 필요합니다.");
            Debug.Assert(Mover != null, $"{name}에는 PlayerMover 모듈이 필요합니다.");
            Debug.Assert(Health != null, $"{name}에는 PlayerHealth 모듈이 필요합니다.");
            Debug.Assert(Weapon != null, $"{name}에는 PlayerWeapon 모듈이 필요합니다.");
            Debug.Assert(ControlFsm != null, $"{name}에는 ControlStateModule이 필요합니다.");
            Debug.Assert(MoveFsm != null, $"{name}에는 MoveStateModule이 필요합니다.");
            Debug.Assert(WeaponFsm != null, $"{name}에는 WeaponStateModule이 필요합니다.");

            if (Weapon != null)
                Weapon.OnWeaponFired += HandleWeaponFired;
        }

        protected override void Start()
        {
            base.Start();

            if (!lockCursor)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (cinemachineCamera != null) currentFOV = cinemachineCamera.Lens.FieldOfView;
        }

        private void Update()
        {
            if (ControlFsm == null)
                return;

            float dt = Time.deltaTime;

            if (playerInput != null)
                playerInput.Fill(Input);
            else
                Input.Clear();
            Health.Tick(dt);
            ControlFsm.Tick(dt);

            UpdateAmmoUI();

            if (ControlFsm.IsGameplayAlive)
            {
                Mover.TickLook(Input.Look);
                MoveFsm.Tick(dt);
                WeaponFsm.Tick(dt);
                Weapon.Tick(dt);
            }
            else
            {
                Mover.SetPlanarInput(Vector2.zero, 0f);
            }

            Mover.TickPhysics(dt);
            PushAnimator();
        }
        private void UpdateAmmoUI()
        {
            if (ammoText == null) return;

            if (Weapon.Weapon is MeleeSawedOffWeapon melee)
            {
                ammoText.gameObject.SetActive(melee.IsShotgunMode);
                if (melee.IsShotgunMode)
                    ammoText.text = melee.ShotgunIsReloading ? "재장전 중..." : $"{melee.ShotgunCurrentAmmo} / {melee.ShotgunMaxAmmo}";
                return;
            }

            if (Weapon.Weapon is SniperSawedOffWeapon sniper)
            {
                ammoText.gameObject.SetActive(true);
                if (sniper.IsShotgunMode)
                    ammoText.text = sniper.ShotgunIsReloading ? "재장전 중..." : $"{sniper.ShotgunCurrentAmmo} / {sniper.ShotgunMaxAmmo}";
                else
                    ammoText.text = sniper.SniperIsReloading ? "재장전 중..." : $"{sniper.SniperCurrentAmmo} / {sniper.SniperMaxAmmo}";
                return;
            }

            bool hasAmmo = Weapon.Weapon is GunDealerWeapon || Weapon.Weapon is LaserDealerWeapon;
            ammoText.gameObject.SetActive(hasAmmo);
            if (!hasAmmo) return;

            ammoText.text = Weapon.Weapon.IsReloading
                ? "재장전 중..."
                : $"{Mathf.CeilToInt(Weapon.Weapon.CurrentResource)} / {Mathf.CeilToInt(Weapon.Weapon.MaxResource)}";
        }

        public void TakeDamage(float amount, GameObject source)
        {
            Health.TakeDamage(amount);
        }

        public void Heal(float amount)
        {
            Health.Heal(amount);
        }

        public void ApplyDamageTo(IDamageable target, float amount, bool isWeakpoint = false)
        {
            target?.TakeDamage(amount, gameObject);
        }

        public void ApplyHealTo(IHealable target, float amount)
        {
            target?.Heal(amount);
        }

        public void SetMoveSpeedMultiplier(float multiplier)
        {
            Mover.SetOwnerSpeedMultiplier(multiplier);
        }

        public void SetWeaponHitboxActive(bool active)
        {
            Weapon.SetHitboxActive(active);
        }

        [ContextMenu("Log FSM States")]
        private void LogFsmStates()
        {
            Debug.Log(
                $"{name} Control={ControlFsm?.Machine.CurrentType?.Name} " +
                $"Move={MoveFsm?.Machine.CurrentType?.Name} " +
                $"Weapon={WeaponFsm?.Machine.CurrentType?.Name}",
                this);
        }

        private void PushAnimator()
        {
            if (Renderer == null) return;

            bool isAimingNow = WeaponFsm.Machine.IsCurrent<AimWeaponState>();
            bool isReloadingNow = WeaponFsm.Machine.IsCurrent<ReloadWeaponState>();

            if (isAimingNow && !wasAimingLastFrame)
                aimEnterPulseTimer = aimEnterPulseDuration;
            wasAimingLastFrame = isAimingNow;

            if (aimEnterPulseTimer > 0f) aimEnterPulseTimer -= Time.deltaTime;
            if (firePulseTimer > 0f) firePulseTimer -= Time.deltaTime;

            bool isBursting = (Weapon.Weapon as GunDealerWeapon)?.IsBursting ?? false;

            bool isAimPulseActive = aimEnterPulseTimer > 0f;
            bool isFirePulseActive = firePulseTimer > 0f || isBursting;

            bool isFireNow = isFirePulseActive && !lastFireWasAimed;
            bool isAimFireNow = isFirePulseActive && lastFireWasAimed;
            bool isAimIdleNow = isAimingNow && !isAimPulseActive && !isFirePulseActive;
            bool isIdleNow = !isAimingNow && !isFirePulseActive && !isReloadingNow;

            if (idleParam != null) Renderer.SetBool(idleParam.HashValue, isIdleNow);
            if (speedParam != null)
            {
                float maxSpeed = Mathf.Max(Mover.RunSpeed, 0.0001f);
                Renderer.SetFloat(speedParam.HashValue, Mover.PlanarSpeed / maxSpeed);
            }

            if (isReloadingNow && !wasReloadingLastFrame && rand_Reload != null)
                Renderer.SetFloat(rand_Reload.HashValue, Random.Range(0, 3));

            wasReloadingLastFrame = isReloadingNow;
            if (groundedParam != null) Renderer.SetBool(groundedParam.HashValue, Mover.IsGrounded);
            if (crouchParam != null) Renderer.SetBool(crouchParam.HashValue, MoveFsm.Capabilities.IsCrouching);
            if (airborneParam != null) Renderer.SetBool(airborneParam.HashValue, MoveFsm.Capabilities.IsAirborne);
            if (isAimParam != null) Renderer.SetBool(isAimParam.HashValue, isAimPulseActive);
            if (aimIdleParam != null) Renderer.SetBool(aimIdleParam.HashValue, isAimIdleNow);
            if (isFireParam != null) Renderer.SetBool(isFireParam.HashValue, isFireNow);
            if (isAimFireParam != null) Renderer.SetBool(isAimFireParam.HashValue, isAimFireNow);
            if (reloadParam != null) Renderer.SetBool(reloadParam.HashValue, isReloadingNow);
        }
        public void ApplyRecoil(float pitchDelta, float yawDelta, float dutchImpulse = 0f) => Weapon.ApplyRecoil(pitchDelta, yawDelta, dutchImpulse);

        private void OnDestroy()
        {
            if (Weapon != null)
                Weapon.OnWeaponFired -= HandleWeaponFired;
        }
        private void HandleWeaponFired(string animId)
        {
            switch (animId)
            {
                case "Gun_Fire":
                case "Sniper_Fire":
                case "SawedOff_FireLeft":
                case "SawedOff_FireRight":
                case "Laser_EnergyBall":
                    firePulseTimer = firePulseDuration;
                    lastFireWasAimed = WeaponFsm.Machine.IsCurrent<AimWeaponState>();
                    break;
            }
        }
    }
}