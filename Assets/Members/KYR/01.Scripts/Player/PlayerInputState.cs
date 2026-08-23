using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Members.KYR._01_Scripts
{
    public sealed class PlayerInputState
    {
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool RunHeld { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool AimHeld { get; private set; }
        public bool ReloadPressed { get; private set; }

        public float MoveSqrMagnitude => Move.sqrMagnitude;

        public bool HasMoveInput => MoveSqrMagnitude > 0.01f;

        public void Collect()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            Vector2 move = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed) move.x -= 1f;
            }

            Move = move.sqrMagnitude > 1f ? move.normalized : move;
            Look = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            JumpPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            CrouchHeld = keyboard != null && keyboard.leftCtrlKey.isPressed;
            RunHeld = keyboard != null && keyboard.leftShiftKey.isPressed;
            FireHeld = mouse != null && mouse.leftButton.isPressed;
            FirePressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
            AimHeld = mouse != null && mouse.rightButton.isPressed;
            ReloadPressed = keyboard != null && keyboard.rKey.wasPressedThisFrame;
#else
            Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (Move.sqrMagnitude > 1f)
                Move = Move.normalized;

            Look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            JumpPressed = Input.GetButtonDown("Jump");
            CrouchHeld = Input.GetKey(KeyCode.LeftControl);
            RunHeld = Input.GetKey(KeyCode.LeftShift);
            FireHeld = Input.GetButton("Fire1");
            FirePressed = Input.GetButtonDown("Fire1");
            AimHeld = Input.GetButton("Fire2");
            ReloadPressed = Input.GetKeyDown(KeyCode.R);
#endif
        }

        public void Clear()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            JumpPressed = false;
            CrouchHeld = false;
            RunHeld = false;
            FireHeld = false;
            FirePressed = false;
            AimHeld = false;
            ReloadPressed = false;
        }
    }
}
