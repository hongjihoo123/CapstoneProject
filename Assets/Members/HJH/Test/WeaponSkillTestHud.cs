using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01_Scripts;
using RobotWeapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.HJH.Test
{
    public class WeaponSkillTestHud : MonoBehaviour
    {
        private const float PanelWidth = 440f;
        private const float RowHeight = 24f;

        [SerializeField] private PlayerAgent player;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tKey.wasPressedThisFrame)
                return;

            foreach (TestDummy dummy in FindObjectsByType<TestDummy>(FindObjectsSortMode.None))
                dummy.ResetDummy();
        }

        private void OnGUI()
        {
            if (player == null || player.Weapon == null)
                return;

            float height = RowHeight * (SkillSlots.Count + 1) + 16f;
            var panel = new Rect((Screen.width - PanelWidth) * 0.5f, Screen.height - height - 12f, PanelWidth, height);

            GUILayout.BeginArea(panel, GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"HP {player.Health.CurrentHealth:0}", GUILayout.Width(PanelWidth * 0.5f));
            GUILayout.Label(GetWeaponInfo());
            GUILayout.EndHorizontal();

            foreach (SkillSlotId slot in SkillSlots.All)
                DrawSkillRow(slot);

            GUILayout.EndArea();
        }

        private void DrawSkillRow(SkillSlotId slot)
        {
            SkillData data = player.SkillFsm.GetSkill(slot);
            float cooldown = player.SkillFsm.GetCooldownRemaining(slot);
            bool active = player.SkillFsm.AnimBlendIndex == (int)slot;
            string state = active ? "사용중" : cooldown > 0f ? $"{cooldown:0.0}s" : "READY";

            GUILayout.BeginHorizontal();
            GUILayout.Label(data != null ? data.name : "-", GUILayout.Width(210f));
            GUILayout.Label(player.GetSkillKeyLabel(slot), GUILayout.Width(60f));
            GUILayout.Label(data != null ? state : "-");
            GUILayout.EndHorizontal();
        }

        private string GetWeaponInfo()
        {
            if (player.Weapon.Weapon is IComboWeapon combo)
                return $"콤보 {combo.ComboIndex + 1}/{combo.ComboCount}";

            if (player.Weapon.Weapon is IAmmoDisplay display && display.TryGetAmmoText(out string ammo))
                return ammo;

            return "-";
        }
    }
}
