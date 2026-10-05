using Members.JJH._02_Scripts.Systems.ModuleSystem;
using RobotWeapons;
using UnityEngine;

namespace Members.KYR._01_Scripts.Modules
{
    public class PlayerWeapon : Module
    {
        [SerializeField] private WeaponData equippedWeaponData;
        [SerializeField] private WeaponHitbox weaponHitbox;
        [SerializeField] private Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillOverlapHitbox skillOverlapHitbox;

        private IWeapon _weapon;
        private WeaponData _lastEquippedData;

        public IWeapon Weapon => _weapon;
        public Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module.SkillOverlapHitbox SkillOverlapHitbox => skillOverlapHitbox;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            var selectedCharacter = CharacterSelectionContext.Selected;
            if (selectedCharacter != null && selectedCharacter.weaponData != null)
                equippedWeaponData = selectedCharacter.weaponData;

            _lastEquippedData = equippedWeaponData;
            if (equippedWeaponData == null)
                return;
            Equip(WeaponFactory.Create(equippedWeaponData));
        }

        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            if (equippedWeaponData == _lastEquippedData) return;

            _lastEquippedData = equippedWeaponData;
            if (equippedWeaponData != null)
                Equip(WeaponFactory.Create(equippedWeaponData));
        }

        public void ApplySelectedCharacter()
        {
            var selected = CharacterSelectionContext.Selected;
            if (selected == null || selected.weaponData == null)
                return;

            equippedWeaponData = selected.weaponData;
            _lastEquippedData = equippedWeaponData;
            Equip(WeaponFactory.Create(equippedWeaponData));
        }

        public void Equip(IWeapon weapon)
        {
            _weapon?.Unequip();
            _weapon = weapon;

            if (_weapon == null || _owner is not IWeaponOwner weaponOwner)
                return;

            _weapon.Equip(weaponOwner);
            weaponHitbox?.Init(_weapon);
        }

        public void Tick(float deltaTime)
        {
            _weapon?.Tick(deltaTime);
        }

        public void SetHitboxActive(bool active)
        {
            weaponHitbox?.SetActive(active);
        }

        public void TriggerSkillOverlapHit()
        {
            if (_owner is Members.KYR._01_Scripts.PlayerAgent player)
                player.SkillFsm.Anim_SkillOverlapHit();
        }

        public void Anim_MuzzleFlash()
        {
            _weapon?.ExecuteHit();
        }
    }
}
