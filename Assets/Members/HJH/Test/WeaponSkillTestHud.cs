using RobotWeapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.HJH.Test
{
    // Test-scene helper: T resets every TestDummy. The on-screen info was replaced by the skill bar HUD.
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
