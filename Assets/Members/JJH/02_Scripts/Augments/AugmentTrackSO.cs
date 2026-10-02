using UnityEngine;

namespace Members.JJH._02_Scripts.Augments
{
    public enum AugmentCategory
    {
        Weapon,
        Q,
        E,
        Passive
    }

    [System.Serializable]
    public class AugmentTierInfo
    {
        [TextArea(2, 4)] public string description;
    }

    // 증강 "한 갈래(트랙)"를 나타내는 데이터. 예: "대쉬 강화" 트랙이 1->2->3단계로 강해짐.
    [CreateAssetMenu(fileName = "New Augment Track", menuName = "SO/Augment/Augment Track")]
    public class AugmentTrackSO : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("저장/비교용 고유 키. 예: Weapon_Magazine, Q_Dash")]
        public string trackId;
        public AugmentCategory category;

        [Header("표시")]
        public string trackName;
        public Sprite icon;

        [Header("단계별 효과 (1단계 -> 2단계 -> 3단계)")]
        public AugmentTierInfo[] tiers = new AugmentTierInfo[3];

        public int MaxTier => tiers != null ? tiers.Length : 0;

        public AugmentTierInfo GetTierInfo(int tier)
        {
            if (tiers == null || tier < 1 || tier > tiers.Length) return null;
            return tiers[tier - 1];
        }
    }
}
