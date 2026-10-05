using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.KYR._01_Scripts
{
    [CreateAssetMenu(fileName = "PlayerInput", menuName = "SO/Player Input")]
    public class PlayerInputSO : ScriptableObject
    {
        private Controls _controls;
        private InputAction _dash;

        private InputAction _skillQ;
        private InputAction _skillE;
        private InputAction _skillX;

        public bool QPressed { get; private set; }
        public bool EPressed { get; private set; }
        public bool XPressed { get; private set; }

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool RunHeld { get; private set; }
        public bool DashPressed { get; private set; }

        private void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new Controls();
                BindDash();
                BindSkillActions();
            }

            _controls.Player.Enable();
            _dash?.Enable();

            _skillQ?.Enable();
            _skillE?.Enable();
            _skillX?.Enable();
        }

        private void OnDisable()
        {
            _dash?.Disable();
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
            RunHeld = _controls.Player.Sprint.IsPressed();
            DashPressed = _dash != null && _dash.WasPressedThisFrame();

            QPressed = _skillQ != null && _skillQ.WasPressedThisFrame();
            EPressed = _skillE != null && _skillE.WasPressedThisFrame();
            XPressed = _skillX != null && _skillX.WasPressedThisFrame();
            state.CopyFrom(this);
        }

        private void BindDash()
        {
            InputActionMap map = _controls.asset.FindActionMap("Player");
            _dash = map.FindAction("Dash");

            if (_dash == null)
            {
                _dash = new InputAction("Dash", InputActionType.Button);
                _dash.AddBinding("<Keyboard>/space");
                _dash.AddBinding("<Gamepad>/buttonEast");
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
