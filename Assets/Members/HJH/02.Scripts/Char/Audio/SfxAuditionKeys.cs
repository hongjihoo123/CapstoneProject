#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    public class SfxAuditionKeys : MonoBehaviour
    {
        public enum Key { Next, Previous, Confirm, SwitchSlot }

        public static event Action<Key> Pressed;

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null)
                return;
            if (k.rightBracketKey.wasPressedThisFrame) Pressed?.Invoke(Key.Next);
            if (k.leftBracketKey.wasPressedThisFrame) Pressed?.Invoke(Key.Previous);
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) Pressed?.Invoke(Key.Confirm);
            if (k.backslashKey.wasPressedThisFrame) Pressed?.Invoke(Key.SwitchSlot);
#else
            if (Input.GetKeyDown(KeyCode.RightBracket)) Pressed?.Invoke(Key.Next);
            if (Input.GetKeyDown(KeyCode.LeftBracket)) Pressed?.Invoke(Key.Previous);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Pressed?.Invoke(Key.Confirm);
            if (Input.GetKeyDown(KeyCode.Backslash)) Pressed?.Invoke(Key.SwitchSlot);
#endif
        }
    }
}
#endif
