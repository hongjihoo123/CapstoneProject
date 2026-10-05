using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01_Scripts.Stats;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerMover : Module
    {
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float turnSpeed = 720f;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 14f, -10f);
        [SerializeField] private Vector3 cameraEuler = new Vector3(45f, 0f, 0f);
        [SerializeField] private float cameraFollow = 12f;
        [SerializeField] private float dashSpeed = 18f;
        [SerializeField] private float dashDuration = 0.18f;
        [SerializeField] private float dashCooldown = 0.45f;

        [Header("스탯 풀백 모듈 없을 때만")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7.5f;

        private float _targetPlanarSpeed;
        private float _verticalVelocity;
        private Vector2 _planarInput;
        private float _planarSpeed;
        private Vector3 _dashDirection;
        private float _dashSpeed;
        private float _dashTimeRemaining;
        private float _dashCooldownRemaining;
        private PlayerStatsModule _stats;

        public float WalkSpeed => GetStat(PlayerStatId.WalkSpeed, walkSpeed);
        public float RunSpeed => GetStat(PlayerStatId.RunSpeed, runSpeed);
        public float OwnerSpeedMultiplier { get; private set; } = 1f;
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsDashing => _dashTimeRemaining > 0f;
        public float PlanarSpeed => new Vector3(characterController.velocity.x, 0f, characterController.velocity.z).magnitude;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _stats = owner.GetModule<PlayerStatsModule>();

            if (characterController == null)
                characterController = owner.GetComponent<CharacterController>();

            Debug.Assert(characterController != null, $"{owner.name}에는 CharacterController가 필요합니다.");

            if (cameraPivot != null && cameraPivot.parent != null)
                cameraPivot.SetParent(null, true);
        }

        public void SetOwnerSpeedMultiplier(float multiplier)
        {
            OwnerSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetPlanarInput(Vector2 input, float speed)
        {
            _planarInput = input.sqrMagnitude > 1f ? input.normalized : input;
            _targetPlanarSpeed = Mathf.Max(0f, speed) * OwnerSpeedMultiplier;
        }

        public void Teleport(Vector3 position, Quaternion? rotation = null)
        {
            bool wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
                characterController.enabled = false;

            _owner.transform.position = position;
            if (rotation.HasValue)
                _owner.transform.rotation = rotation.Value;

            _verticalVelocity = 0f;
            _dashTimeRemaining = 0f;
            _planarInput = Vector2.zero;
            _planarSpeed = 0f;
            _targetPlanarSpeed = 0f;

            if (characterController != null)
                characterController.enabled = wasEnabled;

            Physics.SyncTransforms();
        }

        public void TickPhysics(float deltaTime)
        {
            if (characterController == null)
                return;

            if (_dashCooldownRemaining > 0f)
                _dashCooldownRemaining -= deltaTime;

            _planarSpeed = Mathf.MoveTowards(_planarSpeed, _targetPlanarSpeed, acceleration * deltaTime);

            if (IsGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * deltaTime;

            Vector3 planar;
            if (_dashTimeRemaining > 0f)
            {
                planar = _dashDirection * _dashSpeed;
                _dashTimeRemaining -= deltaTime;
                FaceDirection(_dashDirection, deltaTime);
            }
            else
            {
                planar = GetCameraPlanarDirection(_planarInput) * _planarSpeed;
                if (planar.sqrMagnitude > 0.0001f)
                    FaceDirection(planar, deltaTime);
            }

            Vector3 motion = planar + Vector3.up * _verticalVelocity;
            characterController.Move(motion * deltaTime);

            TickCamera(deltaTime);
        }

        public void TryDash(Vector2 moveInput)
        {
            if (_dashTimeRemaining > 0f || _dashCooldownRemaining > 0f)
                return;

            Vector3 direction = GetCameraPlanarDirection(moveInput);
            Dash(direction, dashSpeed, dashDuration);
            _dashCooldownRemaining = dashCooldown;
        }

        public Vector3 GetCameraPlanarDirection(Vector2 moveInput)
        {
            Transform basis = cameraPivot != null ? cameraPivot : _owner.transform;
            Vector3 forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up);
            Vector3 right = Vector3.ProjectOnPlane(basis.right, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;

            Vector3 direction = right.normalized * moveInput.x + forward.normalized * moveInput.y;
            if (direction.sqrMagnitude < 0.0001f)
                return Vector3.ProjectOnPlane(_owner.transform.forward, Vector3.up).normalized;

            return direction.normalized;
        }

        public void Dash(Vector3 direction, float speed, float duration)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = _owner.transform.forward;

            _dashDirection = direction.normalized;
            _dashSpeed = speed * GetStat(PlayerStatId.DashSpeed, 1f);
            _dashTimeRemaining = duration * GetStat(PlayerStatId.DashDuration, 1f);
        }

        private void FaceDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            _owner.transform.rotation = Quaternion.RotateTowards(
                _owner.transform.rotation,
                target,
                turnSpeed * deltaTime);
        }

        private void TickCamera(float deltaTime)
        {
            if (cameraPivot == null)
                return;

            Vector3 targetPosition = _owner.transform.position + cameraOffset;
            float t = 1f - Mathf.Exp(-cameraFollow * deltaTime);
            cameraPivot.position = Vector3.Lerp(cameraPivot.position, targetPosition, t);
            cameraPivot.rotation = Quaternion.Euler(cameraEuler);
        }

        private float GetStat(PlayerStatId id, float fallback)
        {
            return _stats != null && _stats.Tree != null ? _stats.Get(id) : fallback;
        }
    }
}
