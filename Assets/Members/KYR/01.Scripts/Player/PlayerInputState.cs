using UnityEngine;
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
        public bool QPressed { get; private set; }
        public bool EPressed { get; private set; }
        public bool XPressed { get; private set; }
        public float MoveSqrMagnitude => Move.sqrMagnitude;
        public bool HasMoveInput => MoveSqrMagnitude > 0.01f;
        public void CopyFrom(PlayerInputSO source)
        {
            if (source == null)
            {
                Clear();
                return;
            }
            Move = source.Move;
            Look = source.Look;
            JumpPressed = source.JumpPressed;
            CrouchHeld = source.CrouchHeld;
            RunHeld = source.RunHeld;
            FireHeld = source.FireHeld;
            FirePressed = source.FirePressed;
            AimHeld = source.AimHeld;
            ReloadPressed = source.ReloadPressed;
            QPressed = source.QPressed;
            EPressed = source.EPressed;
            XPressed = source.XPressed;
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
            QPressed = false;
            EPressed = false;
            XPressed = false;
        }
    }
}