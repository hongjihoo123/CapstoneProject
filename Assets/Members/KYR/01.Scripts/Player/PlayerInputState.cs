using UnityEngine;

namespace Members.KYR._01_Scripts
{
    public sealed class PlayerInputState
    {
        public Vector2 Move { get; private set; }
        public bool RunHeld { get; private set; }
        public bool DashPressed { get; private set; }
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
            RunHeld = source.RunHeld;
            DashPressed = source.DashPressed;
            QPressed = source.QPressed;
            EPressed = source.EPressed;
            XPressed = source.XPressed;
        }

        public void Clear()
        {
            Move = Vector2.zero;
            RunHeld = false;
            DashPressed = false;
            QPressed = false;
            EPressed = false;
            XPressed = false;
        }
    }
}
