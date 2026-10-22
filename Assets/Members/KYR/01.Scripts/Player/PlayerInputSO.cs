using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.KYR._01_Scripts
{
    [CreateAssetMenu(fileName = "PlayerInput", menuName = "SO/Player Input")]
    public class PlayerInputSO : ScriptableObject
    {
        private static readonly (SkillSlotId slot, string binding)[] DefaultSkillBindings =
        {
            (SkillSlotId.Dash, "<Keyboard>/space"),
            (SkillSlotId.Weapon, "<Mouse>/rightButton"),
            (SkillSlotId.Ultimate, "<Keyboard>/r"),
            (SkillSlotId.Basic1, "<Keyboard>/q"),
            (SkillSlotId.Basic2, "<Keyboard>/e"),
        };

        private readonly Dictionary<SkillSlotId, InputAction> _skillActions = new();
        private readonly bool[] _skillPressed = new bool[SkillSlots.Count];
        private readonly bool[] _skillHeld = new bool[SkillSlots.Count];
        private readonly bool[] _skillReleased = new bool[SkillSlots.Count];

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
        public bool AimPressed { get; private set; }
        public bool ReloadPressed { get; private set; }

        public bool WasSkillPressed(SkillSlotId slot) => _skillPressed[(int)slot];
        public bool IsSkillHeld(SkillSlotId slot) => _skillHeld[(int)slot];
        public bool WasSkillReleased(SkillSlotId slot) => _skillReleased[(int)slot];

        public string GetSkillBindingLabel(SkillSlotId slot) =>
            _skillActions.TryGetValue(slot, out InputAction action) ? action.GetBindingDisplayString() : string.Empty;

        private void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new Controls();
                BindAimReload();
                BindSkillActions();
            }

            _controls.Player.Enable();
            _aim?.Enable();
            _reload?.Enable();

            foreach (InputAction action in _skillActions.Values)
                action.Enable();
        }

        private void OnDisable()
        {
            _aim?.Disable();
            _reload?.Disable();

            foreach (InputAction action in _skillActions.Values)
                action.Disable();

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
            AimPressed = _aim != null && _aim.WasPressedThisFrame();
            ReloadPressed = _reload != null && _reload.WasPressedThisFrame();

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                _skillActions.TryGetValue(slot, out InputAction action);
                _skillPressed[(int)slot] = action != null && action.WasPressedThisFrame();
                _skillHeld[(int)slot] = action != null && action.IsPressed();
                _skillReleased[(int)slot] = action != null && action.WasReleasedThisFrame();
            }

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

            // Top-down layout: Space = dash (also cancels aiming), right click = weapon skill, R = ultimate.
            // Weapon aim loses right click, jump loses Space, and manual reload is removed (dash reloads instantly).
            DisableBinding(_aim, "<Mouse>/rightButton");
            DisableBinding(map.FindAction("Jump"), "<Keyboard>/space");
            _reload = null;
        }

        private static void DisableBinding(InputAction action, string path)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].path == path)
                    action.ApplyBindingOverride(i, string.Empty);
            }
        }

        private void BindSkillActions()
        {
            InputActionMap map = _controls.asset.FindActionMap("Player");
            _skillActions.Clear();

            foreach ((SkillSlotId slot, string binding) in DefaultSkillBindings)
            {
                string actionName = $"Skill{slot}";
                InputAction action = map.FindAction(actionName);
                if (action == null)
                {
                    action = new InputAction(actionName, InputActionType.Button);
                    action.AddBinding(binding);
                }

                _skillActions[slot] = action;
            }
        }
    }
}
