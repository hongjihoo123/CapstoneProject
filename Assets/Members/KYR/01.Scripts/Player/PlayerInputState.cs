using System;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using UnityEngine;

namespace Members.KYR._01_Scripts
{
    public sealed class PlayerInputState : ISkillInputSource
    {
        private readonly bool[] _skillPressed = new bool[SkillSlots.Count];
        private readonly bool[] _skillHeld = new bool[SkillSlots.Count];
        private readonly bool[] _skillReleased = new bool[SkillSlots.Count];

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
        public float MoveSqrMagnitude => Move.sqrMagnitude;
        public bool HasMoveInput => MoveSqrMagnitude > 0.01f;

        public bool WasSkillPressed(SkillSlotId slot) => _skillPressed[(int)slot];
        public bool IsSkillHeld(SkillSlotId slot) => _skillHeld[(int)slot];
        public bool WasSkillReleased(SkillSlotId slot) => _skillReleased[(int)slot];
        public bool WasCancelPressed() => AimPressed;
        public bool WasJumpPressed() => JumpPressed;

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
            AimPressed = source.AimPressed;
            ReloadPressed = source.ReloadPressed;

            foreach (SkillSlotId slot in SkillSlots.All)
            {
                _skillPressed[(int)slot] = source.WasSkillPressed(slot);
                _skillHeld[(int)slot] = source.IsSkillHeld(slot);
                _skillReleased[(int)slot] = source.WasSkillReleased(slot);
            }
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
            AimPressed = false;
            ReloadPressed = false;
            Array.Clear(_skillPressed, 0, _skillPressed.Length);
            Array.Clear(_skillHeld, 0, _skillHeld.Length);
            Array.Clear(_skillReleased, 0, _skillReleased.Length);
        }
    }
}
