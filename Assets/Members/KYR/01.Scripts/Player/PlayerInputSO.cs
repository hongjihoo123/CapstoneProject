using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.KYR._01_Scripts
{
    [CreateAssetMenu(fileName = "PlayerInput", menuName = "SO/Player Input")]
    public class PlayerInputSO : ScriptableObject
    {
        private Controls _controls;
        private InputAction _aim;
        private InputAction _reload;

        private InputAction _skillQ;
        private InputAction _skillE;
        private InputAction _skillX;

        public bool QPressed { get; private set; }
        public bool EPressed { get; private set; }
        public bool XPressed { get; private set; }

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
                BindSkillActions();
                // 콜백(AddCallbacks) 방식은 일부러 안 씀 - Fill()의 폴링 방식이랑
                // 동시에 같은 값을 따로 덮어쓰면서 레이스 컨디션이 생겨서
                // (클릭 한 번이 두 프레임에 걸쳐 중복 감지되는 등) 제거함.
            }

            _controls.Player.Enable();
            _aim?.Enable();
            _reload?.Enable();

            _skillQ?.Enable();
            _skillE?.Enable();
            _skillX?.Enable();
        }

        private void OnDisable()
        {
            _aim?.Disable();
            _reload?.Disable();
            _skillQ?.Disable();
            _skillE?.Disable();
            _skillX?.Disable();
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

            QPressed = _skillQ != null && _skillQ.WasPressedThisFrame();
            EPressed = _skillE != null && _skillE.WasPressedThisFrame();
            XPressed = _skillX != null && _skillX.WasPressedThisFrame();
            state.CopyFrom(this);
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

        private void BindSkillActions()
        {
            InputActionMap map = _controls.asset.FindActionMap("Player");

            _skillQ = map.FindAction("SkillQ");
            if (_skillQ == null)
            {
                _skillQ = new InputAction("SkillQ", InputActionType.Button);
                _skillQ.AddBinding("<Keyboard>/q");
            }

            _skillE = map.FindAction("SkillE");
            if (_skillE == null)
            {
                _skillE = new InputAction("SkillE", InputActionType.Button);
                _skillE.AddBinding("<Keyboard>/e");
            }

            _skillX = map.FindAction("SkillX");
            if (_skillX == null)
            {
                _skillX = new InputAction("SkillX", InputActionType.Button);
                _skillX.AddBinding("<Keyboard>/x");
            }
        }
    }
}