using Members.JJH._02_Scripts.Agents;
using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using Members.KYR._01_Scripts.FSM.Control;
using Members.KYR._01_Scripts.FSM.Move;
using Assets.Members.HJH._02.Scripts.Char;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts.FSM.Weapon;
using Members.KYR._01_Scripts.Modules;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01_Scripts
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerAgent : Agent, IWeaponOwner, IDamageable, IHealable,IRecoilCapable
    {

        [SerializeField] private PlayerInputSO playerInput;
        [SerializeField] private WeaponKitData kit;

        [Header("총 관련")]
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform muzzleOrigin;
        [SerializeField] private Text ammoText;
        [Header("반동 관련")]
        [SerializeField] private CinemachineCamera cinemachineCamera;

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
        [SerializeField] private AnimParamSO isSkillParam;
        [SerializeField] private AnimParamSO skillBlendParam;

        [SerializeField] private float aimEnterPulseDuration = 0.15f;
        [SerializeField] private float firePulseDuration = 0.15f;

        private bool wasAimingLastFrame;
        private float aimEnterPulseTimer;
        private float firePulseTimer;
        private bool lastFireWasAimed;
        private bool wasReloadingLastFrame;


        public PlayerInputState Input { get; } = new();
        public PlayerMover Mover { get; private set; }
        public new PlayerHealth Health { get; private set; }
        public PlayerStatsModule Stats { get; private set; }
        public PlayerWeapon Weapon { get; private set; }
        public ControlStateModule ControlFsm { get; private set; }
        public MoveStateModule MoveFsm { get; private set; }
        public WeaponStateModule WeaponFsm { get; private set; }
        public SkillStateModule SkillFsm { get; private set; }
        public StatusEffectModule StatusEffects { get; private set; }

        public Transform AimOrigin => aimOrigin != null ? aimOrigin : transform;
        public Transform MuzzleOrigin => muzzleOrigin != null ? muzzleOrigin : transform;
        public override bool IsAlive => Health != null && !Health.IsDead;
        public CinemachineCamera CinemachineCamera => cinemachineCamera;

        protected override void InitializeModules()
        {
            base.InitializeModules();
            AimUtility.IgnoreLayerMask = LayerMask.GetMask("Player");

            Mover = GetModule<PlayerMover>();
            Health = GetModule<PlayerHealth>();
            Stats = GetModule<PlayerStatsModule>();
            Weapon = GetModule<PlayerWeapon>();
            ControlFsm = GetModule<ControlStateModule>();
            MoveFsm = GetModule<MoveStateModule>();
            WeaponFsm = GetModule<WeaponStateModule>();
            SkillFsm = GetModule<SkillStateModule>();
            StatusEffects = GetModule<StatusEffectModule>();

            Debug.Assert(playerInput != null, $"{name}에는 PlayerInputSO가 필요합니다.");
            Debug.Assert(Mover != null, $"{name}에는 PlayerMover 모듈이 필요합니다.");
            Debug.Assert(Health != null, $"{name}에는 PlayerHealth 모듈이 필요합니다.");
            Debug.Assert(Stats != null, $"{name}에는 PlayerStatsModule이 필요합니다.");
            Debug.Assert(Weapon != null, $"{name}에는 PlayerWeapon 모듈이 필요합니다.");
            Debug.Assert(ControlFsm != null, $"{name}에는 ControlStateModule이 필요합니다.");
            Debug.Assert(MoveFsm != null, $"{name}에는 MoveStateModule이 필요합니다.");
            Debug.Assert(WeaponFsm != null, $"{name}에는 WeaponStateModule이 필요합니다.");
            Debug.Assert(SkillFsm != null, $"{name}에는 SkillStateModule이 필요합니다.");

            if (Weapon != null)
                Weapon.OnWeaponFired += HandleWeaponFired;

            if (kit != null)
                EquipKit(kit);
        }

        public WeaponKitData Kit => kit;

        private Vector3 _aimDirection;
        public Vector3 AimDirection => _aimDirection.sqrMagnitude > 0.0001f ? _aimDirection : transform.forward;

        public void SetAimDirection(Vector3 direction) => _aimDirection = direction;

        private Vector3 _aimPoint;
        private bool _hasAimPoint;
        public Vector3 AimPoint => _hasAimPoint ? _aimPoint : transform.position + AimDirection * 1000f;

        public void SetAimPoint(Vector3 point)
        {
            _aimPoint = point;
            _hasAimPoint = true;
        }

        public string GetSkillKeyLabel(SkillSlotId slot) =>
            playerInput != null ? playerInput.GetSkillBindingLabel(slot) : string.Empty;

        public void EquipKit(WeaponKitData newKit)
        {
            kit = newKit;
            Weapon.EquipData(newKit != null ? newKit.weapon : null);
            SkillFsm.EquipKit(newKit);
        }

        protected override void Start()
        {
            base.Start();

            if (!lockCursor)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
            Stats.Tick(dt);
            Health.Tick(dt);
            ControlFsm.Tick(dt);

            UpdateAmmoUI();

            if (ControlFsm.IsGameplayAlive)
            {
                Mover.TickLook(Input.Look);
                MoveFsm.Tick(dt);
                WeaponFsm.Tick(dt);
                SkillFsm.ResolveInput();
                SkillFsm.Tick(dt);
                Weapon.Tick(dt);
                StatusEffects?.Tick(dt);
            }
            else
            {
                SkillFsm.CancelAim();
                Mover.SetPlanarInput(Vector2.zero, 0f);
            }

            Mover.TickPhysics(dt);
            PushAnimator();
        }

        private void UpdateAmmoUI()
        {
            if (ammoText == null) return;

            string text = null;
            bool showAmmo = Weapon.Weapon is IAmmoDisplay display && display.TryGetAmmoText(out text);

            ammoText.gameObject.SetActive(showAmmo);
            if (showAmmo)
                ammoText.text = text;
        }

        public override void TakeDamage(float amount, GameObject source)
        {
            Health.TakeDamage(amount);
        }

        public void Heal(float amount)
        {
            Health.Heal(amount);
        }

        public void ApplyDamageTo(IDamageable target, float amount, bool isWeakpoint = false)
        {
            float multiplier = Stats != null ? Stats.Get(PlayerStatId.Damage) : 1f;
            if (isWeakpoint && Stats != null)
                multiplier *= Stats.Get(PlayerStatId.WeakpointMultiplier);
            target?.TakeDamage(amount * multiplier, gameObject);
        }

        public void ApplyHealTo(IHealable target, float amount)
        {
            target?.Heal(amount);
        }

        private void OnEnable()
        {
            UiFocusService.OnUiFocusChanged += HandleUiFocusChanged;
        }

        private void OnDisable()
        {
            UiFocusService.OnUiFocusChanged -= HandleUiFocusChanged;
        }

        private void HandleUiFocusChanged(bool uiFocused)
        {
            if (ControlFsm == null)
                return;

            if (uiFocused)
            {
                ControlFsm.ChangeState<UiControlState>();
                return;
            }

            if (!Health.IsDead)
                ControlFsm.ChangeState<AliveControlState>();
        }

        public void SetUiMode(bool uiOpen)
        {
            if (uiOpen)
                UiFocusService.Acquire(this);
            else
                UiFocusService.Release(this);
        }

        public void Teleport(Vector3 position, Quaternion? rotation = null)
        {
            Mover?.Teleport(position, rotation);
        }

        public void SetMoveSpeedMultiplier(float multiplier)
        {
            Mover.SetOwnerSpeedMultiplier(multiplier);
        }

        public void SetWeaponHitboxActive(bool active)
        {
            Weapon.SetHitboxActive(active);
        }
        public void OnEnemyKilled()
        {
            SkillFsm.NotifyEnemyKilled();   
        }

        public void Anim_SkillHitBegin() => SkillFsm.Anim_SkillHitBegin();
        public void Anim_SkillHitEnd() => SkillFsm.Anim_SkillHitEnd();
        public void Anim_SkillHitboxOn() => Weapon.Anim_SkillHitboxOn();
        public void Anim_SkillHitboxOff() => Weapon.Anim_SkillHitboxOff();

        [ContextMenu("Log FSM States")]
        private void LogFsmStates()
        {
            Debug.Log(
                $"{name} Control={ControlFsm?.Machine.CurrentType?.Name} " +
                $"Move={MoveFsm?.Machine.CurrentType?.Name} " +
                $"Weapon={WeaponFsm?.Machine.CurrentType?.Name} " +
                $"Skill={SkillFsm?.Machine.CurrentType?.Name}",
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

            bool isBursting = Weapon.Weapon is IBurstWeapon { IsBursting: true };

            bool isAimPulseActive = aimEnterPulseTimer > 0f;
            bool isFirePulseActive = firePulseTimer > 0f || isBursting;

            bool isFireNow = isFirePulseActive && !lastFireWasAimed;
            bool isAimFireNow = isFirePulseActive && lastFireWasAimed;
            bool isSkillPlayingNow = !SkillFsm.Machine.IsCurrent<IdleSkillState>();

            bool isAimIdleNow = isAimingNow && !isAimPulseActive && !isFirePulseActive;
            bool isIdleNow = !isAimingNow && !isFirePulseActive && !isReloadingNow && !isSkillPlayingNow;

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

            if (isSkillParam != null) Renderer.SetBool(isSkillParam.HashValue, isSkillPlayingNow);
            if (skillBlendParam != null) Renderer.SetFloat(skillBlendParam.HashValue, SkillFsm.AnimBlendIndex);
        }
        public void ApplyRecoil(float pitchDelta, float yawDelta, float dutchImpulse = 0f) => Weapon.ApplyRecoil(pitchDelta, yawDelta, dutchImpulse);

        private void OnDestroy()
        {
            if (Weapon != null)
                Weapon.OnWeaponFired -= HandleWeaponFired;
        }
        private void HandleWeaponFired(AttackFeedback feedback)
        {
            if (!feedback.IsFire)
                return;

            firePulseTimer = firePulseDuration;
            lastFireWasAimed = WeaponFsm.Machine.IsCurrent<AimWeaponState>();
        }
    }
}