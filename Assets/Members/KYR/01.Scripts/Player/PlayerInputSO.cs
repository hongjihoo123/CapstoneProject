using System;
using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.KYR._01_Scripts
{
    [CreateAssetMenu(fileName = "PlayerInput", menuName = "SO/Player Input")]
    public class PlayerInputSO : ScriptableObject, Controls.IPlayerActions
    {
        private static readonly (SkillSlotId slot, string binding)[] DefaultSkillBindings =
        {
            (SkillSlotId.Dash, "<Keyboard>/space"),
            (SkillSlotId.Weapon, "<Mouse>/rightButton"),
            (SkillSlotId.Ultimate, "<Keyboard>/r"),
            (SkillSlotId.Basic1, "<Keyboard>/q"),
            (SkillSlotId.Basic2, "<Keyboard>/e"),
        };

        public event Action OnAttackPressed;
        public event Action OnAttackReleased;
        public event Action OnJumpPressed;
        public event Action OnInteractPressed;
        public event Action OnAimPressedEvent;

        private readonly Dictionary<SkillSlotId, InputAction> _skillActions = new();
        private readonly bool[] _skillPressed = new bool[SkillSlots.Count];
        private readonly bool[] _skillHeld = new bool[SkillSlots.Count];
        private readonly bool[] _skillReleased = new bool[SkillSlots.Count];

        private Controls _controls;

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
                _controls.Player.SetCallbacks(this);
                ApplyTopDownBindings();
                BindSkillActions();
            }

            _controls.Player.Enable();

            foreach (InputAction action in _skillActions.Values)
                action.Enable();
        }

        private void OnDisable()
        {
            foreach (InputAction action in _skillActions.Values)
                action.Disable();

            _controls?.Player.Disable();
            ResetHeldValues();
        }

        #region Controls.IPlayerActions

        public void OnMove(InputAction.CallbackContext context) => Move = context.ReadValue<Vector2>();

        public void OnLook(InputAction.CallbackContext context) => Look = context.ReadValue<Vector2>();

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                FireHeld = true;
                OnAttackPressed?.Invoke();
            }
            else if (context.canceled)
            {
                FireHeld = false;
                OnAttackReleased?.Invoke();
            }
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
                OnInteractPressed?.Invoke();
        }

        public void OnCrouch(InputAction.CallbackContext context) => CrouchHeld = context.ReadValueAsButton();

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
                OnJumpPressed?.Invoke();
        }

        public void OnSprint(InputAction.CallbackContext context) => RunHeld = context.ReadValueAsButton();

        public void OnAim(InputAction.CallbackContext context)
        {
            AimHeld = context.ReadValueAsButton();
            if (context.performed)
                OnAimPressedEvent?.Invoke();
        }

        // 탑다운에서는 수동 장전 없음 (대시하면 즉시 장전)
        public void OnReload(InputAction.CallbackContext context) { }

        #endregion

        public void Fill(PlayerInputState state)
        {
            if (_controls == null)
            {
                state.Clear();
                return;
            }

            Controls.PlayerActions player = _controls.Player;
            JumpPressed = player.Jump.WasPressedThisFrame();
            FirePressed = player.Attack.WasPressedThisFrame();
            AimPressed = player.Aim.WasPressedThisFrame();
            ReloadPressed = false;

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                _skillActions.TryGetValue(slot, out InputAction action);
                _skillPressed[(int)slot] = action != null && action.WasPressedThisFrame();
                _skillHeld[(int)slot] = action != null && action.IsPressed();
                _skillReleased[(int)slot] = action != null && action.WasReleasedThisFrame();
            }

            state.CopyFrom(this);
        }

        private void ResetHeldValues()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            CrouchHeld = false;
            RunHeld = false;
            FireHeld = false;
            AimHeld = false;
        }

         private void ApplyTopDownBindings()
        {
            Controls.PlayerActions player = _controls.Player;
            DisableBinding(player.Aim, "<Mouse>/rightButton");
            DisableBinding(player.Jump, "<Keyboard>/space");
            DisableBinding(player.Crouch, "<Keyboard>/c");
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
