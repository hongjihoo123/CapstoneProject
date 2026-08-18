using UnityEngine;

namespace RobotWeapons
{
    [CreateAssetMenu(menuName = "Weapon/Upgrade Data", fileName = "New UpgradeData")]
    public class UpgradeData : ScriptableObject
    {
        public string upgradeName = "New Upgrade";
        [TextArea] public string description;

        [Header("적용 대상 무기 (비워두면 전체 적용)")]
        public WeaponType[] applicableTypes;

        [Header("증가 수치")]
        public float damageAdd;
        public float healAdd;
        public float resourceAdd;

        [Header("스킬 해금 (선택)")]
        public string unlockSkillId;
    }
}
