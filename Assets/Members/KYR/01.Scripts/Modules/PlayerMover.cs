using Members.JJH._02_Scripts.Systems.ModuleSystem;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerMover : Module
    {
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7.5f;
        [SerializeField] private float crouchSpeed = 2.2f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float airControl = 0.7f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private float crouchHeight = 1.2f;
        [SerializeField] private float acceleration = 18f;

        private float _targetPlanarSpeed;

        private float _verticalVelocity;
        private float _standingHeight;
        private Vector3 _standingCenter;
        private float _pitch;
        private Vector2 _planarInput;
        private float _planarSpeed;
        private Vector3 _hipCameraLocalPosition;
        private bool _hipCameraPositionCaptured;

        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float CrouchSpeed => crouchSpeed;
        public float AirControl => airControl;
        public float OwnerSpeedMultiplier { get; private set; } = 1f;
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public float PlanarSpeed => new Vector3(characterController.velocity.x, 0f, characterController.velocity.z).magnitude;
        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            if (characterController == null)
                characterController = owner.GetComponent<CharacterController>();

            Debug.Assert(characterController != null, $"{owner.name}에는 CharacterController가 필요합니다.");

            _standingHeight = characterController.height;
            _standingCenter = characterController.center;

            if (cameraPivot != null)
            {
                _hipCameraLocalPosition = cameraPivot.localPosition;
                _hipCameraPositionCaptured = true;
            }
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

        public void Jump()
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        public void SetCrouching(bool crouch)
        {
            if (characterController == null)
                return;

            float targetHeight = crouch ? crouchHeight : _standingHeight;
            if (Mathf.Approximately(characterController.height, targetHeight))
                return;

            characterController.height = targetHeight;
            float heightDelta = _standingHeight - targetHeight;
            characterController.center = crouch
                ? _standingCenter - new Vector3(0f, heightDelta * 0.5f, 0f)
                : _standingCenter;
        }

        public void ApplyRecoilPitch(float pitchDelta)
        {
            if (cameraPivot == null)
                return;

            _pitch = Mathf.Clamp(_pitch - pitchDelta, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void ApplyRecoilYaw(float yawDelta)
        {
            _owner.transform.Rotate(0f, yawDelta, 0f);
        }

        public Vector3 HipCameraLocalPosition => _hipCameraPositionCaptured ? _hipCameraLocalPosition : Vector3.zero;

        public void SetCameraLocalPositionInstant(Vector3 localPosition)
        {
            if (cameraPivot == null)
                return;
            cameraPivot.localPosition = localPosition;
        }

        public void ResetCameraToHipInstant()
        {
            if (cameraPivot == null || !_hipCameraPositionCaptured)
                return;
            cameraPivot.localPosition = _hipCameraLocalPosition;
        }

        public void TickLook(Vector2 lookDelta)
        {
            if (cameraPivot == null)
                return;

            Transform root = _owner.transform;
            root.Rotate(0f, lookDelta.x * lookSensitivity, 0f);

            _pitch = Mathf.Clamp(_pitch - lookDelta.y * lookSensitivity, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void TickPhysics(float deltaTime)
        {
            if (characterController == null)
                return;

            _planarSpeed = Mathf.MoveTowards(_planarSpeed, _targetPlanarSpeed, acceleration * deltaTime); // 추가

            if (IsGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * deltaTime;

            Vector3 planar = (_owner.transform.right * _planarInput.x + _owner.transform.forward * _planarInput.y)
                             * _planarSpeed;
            Vector3 motion = planar + Vector3.up * _verticalVelocity;
            characterController.Move(motion * deltaTime);
        }
    }
}