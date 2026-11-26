using RobotWeapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.HJH.Test
{
    public class WeaponSkillTestHud : MonoBehaviour
    {
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tKey.wasPressedThisFrame)
                return;

            foreach (TestDummy dummy in FindObjectsByType<TestDummy>(FindObjectsSortMode.None))
                dummy.ResetDummy();
        }
    }
}
