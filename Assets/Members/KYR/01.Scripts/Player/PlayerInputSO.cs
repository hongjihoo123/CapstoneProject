using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.KYR._01_Scripts
{
    [CreateAssetMenu(fileName = "PlayerInput", menuName = "SO/Player Input")]
    public class PlayerInputSO : ScriptableObject, Controls.IPlayerActions
    {
        private Controls _controls;
        private InputAction _aim;
        private InputAction _reload;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool RunHeld { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool AimHeld { get; private set; }
        public bool ReloadPressed { get; private set; }

        private void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new Controls();
                BindAimReload();
                _controls.Player.AddCallbacks(this);
            }

            _controls.Player.Enable();
            _aim?.Enable();
            _reload?.Enable();
        }

        private void OnDisable()
        {
            _aim?.Disable();
            _reload?.Disable();
            _controls?.Player.Disable();
        }

        public void Fill(PlayerInputState state)
        {
            if (_controls == null)
            {
                state.Clear();
                return;
            }

            Move = _controls.Player.Move.ReadValue<Vector2>();
            Look = _controls.Player.Look.ReadValue<Vector2>();
            JumpPressed = _controls.Player.Jump.WasPressedThisFrame();
            CrouchHeld = _controls.Player.Crouch.IsPressed();
            RunHeld = _controls.Player.Sprint.IsPressed();
            FireHeld = _controls.Player.Attack.IsPressed();
            FirePressed = _controls.Player.Attack.WasPressedThisFrame();
            AimHeld = _aim != null && _aim.IsPressed();
            ReloadPressed = _reload != null && _reload.WasPressedThisFrame();

            state.CopyFrom(this);
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            Move = context.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            Look = context.ReadValue<Vector2>();
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            FireHeld = context.ReadValue<float>() > 0.5f;
            if (context.performed)
                FirePressed = true;
        }

        public void OnInteract(InputAction.CallbackContext context) { }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            CrouchHeld = context.ReadValue<float>() > 0.5f;
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
                JumpPressed = true;
        }

        public void OnPrevious(InputAction.CallbackContext context) { }

        public void OnNext(InputAction.CallbackContext context) { }

        public void OnSprint(InputAction.CallbackContext context)
        {
            RunHeld = context.ReadValue<float>() > 0.5f;
        }

        public void OnAim(InputAction.CallbackContext context)
        {
            AimHeld = context.ReadValue<float>() > 0.5f;
        }

        public void OnReload(InputAction.CallbackContext context)
        {
            if (context.performed)
                ReloadPressed = true;
        }

        private void BindAimReload()
        {
            InputActionMap map = _controls.asset.FindActionMap("Player");
            _aim = map.FindAction("Aim");
            _reload = map.FindAction("Reload");

            if (_aim == null)
            {
                _aim = new InputAction("Aim", InputActionType.Button);
                _aim.AddBinding("<Mouse>/rightButton");
                _aim.AddBinding("<Gamepad>/leftTrigger");
            }

            if (_reload == null)
            {
                _reload = new InputAction("Reload", InputActionType.Button);
                _reload.AddBinding("<Keyboard>/r");
                _reload.AddBinding("<Gamepad>/leftShoulder");
            }
        }
    }
}
