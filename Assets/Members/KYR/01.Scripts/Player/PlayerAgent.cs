using Members.JJH._02_Scripts.Agents;
using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using Members.KYR._01_Scripts.FSM.Control;
using Members.KYR._01_Scripts.FSM.Move;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts.Modules;
using Members.KYR._01_Scripts.Stats;
using RobotWeapons;
using UnityEngine;

namespace Members.KYR._01_Scripts
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerAgent : Agent, IWeaponOwner, IDamageable, IHealable, IRecoilCapable
    {
        [SerializeField] private PlayerInputSO playerInput;

        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform muzzleOrigin;

        [Header("Animator")]
        [SerializeField] private AnimParamSO idleParam;
        [SerializeField] private AnimParamSO speedParam;
        [SerializeField] private AnimParamSO groundedParam;
        [SerializeField] private AnimParamSO isSkillParam;
        [SerializeField] private AnimParamSO skillBlendParam;

        public PlayerInputState Input { get; } = new();
        public PlayerMover Mover { get; private set; }
        public new PlayerHealth Health { get; private set; }
        public PlayerStatsModule Stats { get; private set; }
        public PlayerWeapon Weapon { get; private set; }
        public ControlStateModule ControlFsm { get; private set; }
        public MoveStateModule MoveFsm { get; private set; }
        public SkillStateModule SkillFsm { get; private set; }

        public Transform AimOrigin => aimOrigin != null ? aimOrigin : transform;
        public Transform MuzzleOrigin => muzzleOrigin != null ? muzzleOrigin : transform;
        public override bool IsAlive => Health != null && !Health.IsDead;

        protected override void InitializeModules()
        {
            base.InitializeModules();

            Mover = GetModule<PlayerMover>();
            Health = GetModule<PlayerHealth>();
            Stats = GetModule<PlayerStatsModule>();
            Weapon = GetModule<PlayerWeapon>();
            ControlFsm = GetModule<ControlStateModule>();
            MoveFsm = GetModule<MoveStateModule>();
            SkillFsm = GetModule<SkillStateModule>();

            Debug.Assert(playerInput != null, $"{name}에는 PlayerInputSO가 필요합니다.");
            Debug.Assert(Mover != null, $"{name}에는 PlayerMover 모듈이 필요합니다.");
            Debug.Assert(Health != null, $"{name}에는 PlayerHealth 모듈이 필요합니다.");
            Debug.Assert(Stats != null, $"{name}에는 PlayerStatsModule이 필요합니다.");
            Debug.Assert(Weapon != null, $"{name}에는 PlayerWeapon 모듈이 필요합니다.");
            Debug.Assert(ControlFsm != null, $"{name}에는 ControlStateModule이 필요합니다.");
            Debug.Assert(MoveFsm != null, $"{name}에는 MoveStateModule이 필요합니다.");
            Debug.Assert(SkillFsm != null, $"{name}에는 SkillStateModule이 필요합니다.");
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

            if (ControlFsm.IsGameplayAlive)
            {
                if (Input.DashPressed)
                    Mover.TryDash(Input.Move);

                MoveFsm.Tick(dt);
                SkillFsm.ResolveInput();
                SkillFsm.Tick(dt);
                Weapon.Tick(dt);
            }
            else
            {
                Mover.SetPlanarInput(Vector2.zero, 0f);
            }

            Mover.TickPhysics(dt);
            PushAnimator();
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

        public void Teleport(Vector3 position, Quaternion? rotation = null)
        {
            Mover?.Teleport(position, rotation);
        }

        public void ApplySelectedCharacter()
        {
            Weapon?.ApplySelectedCharacter();
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

        [ContextMenu("Log FSM States")]
        private void LogFsmStates()
        {
            Debug.Log(
                $"{name} Control={ControlFsm?.Machine.CurrentType?.Name} " +
                $"Move={MoveFsm?.Machine.CurrentType?.Name} " +
                $"Skill={SkillFsm?.Machine.CurrentType?.Name}",
                this);
        }

        private void PushAnimator()
        {
            if (Renderer == null) return;

            bool isSkillPlayingNow = !SkillFsm.Machine.IsCurrent<IdleSkillState>();
            bool isIdleNow = !isSkillPlayingNow;

            if (idleParam != null) Renderer.SetBool(idleParam.HashValue, isIdleNow);
            if (speedParam != null)
            {
                float maxSpeed = Mathf.Max(Mover.RunSpeed, 0.0001f);
                Renderer.SetFloat(speedParam.HashValue, Mover.PlanarSpeed / maxSpeed);
            }

            if (groundedParam != null) Renderer.SetBool(groundedParam.HashValue, Mover.IsGrounded);
            if (isSkillParam != null) Renderer.SetBool(isSkillParam.HashValue, isSkillPlayingNow);
            if (skillBlendParam != null) Renderer.SetFloat(skillBlendParam.HashValue, SkillFsm.AnimBlendIndex);
        }

        public void ApplyRecoil(float pitchDelta, float yawDelta, float dutchImpulse = 0f)
        {
        }
    }
}
