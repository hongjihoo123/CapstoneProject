#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Audio
{
    // 소리 고르기 창(에디터)이 플레이 중에만 붙이는 키 입력기. 게임 화면에 포커스가 있어도 키를 받음.
    //   ]  다음 후보   [  이전 후보   Enter  지금 소리로 확정   \  최근 칸끼리 바꾸기 (시전 ↔ 폭발)
    // 에디터 전용 (빌드에는 안 들어감)
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
