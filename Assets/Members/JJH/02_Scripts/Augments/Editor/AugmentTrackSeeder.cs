#if UNITY_EDITOR
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using UnityEditor;
using UnityEngine;

namespace Members.JJH._02_Scripts.Augments.Editor
{
    // 메뉴 한 번으로 기본 증강 트랙 8종 + 이벤트 채널 에셋을 생성/갱신한다.
    // 이미 만들어져 있으면 값만 덮어써서 갱신한다 (새로 만들지 않음).
    public static class AugmentTrackSeeder
    {
        private const string TrackFolder = "Assets/Members/HJH/05.Data/SO/Augment Data";
        private const string ChannelPath = TrackFolder + "/AugmentEventChannel.asset";

        [MenuItem("Tools/Augment/Create Default Augment Tracks And Channel")]
        public static void CreateDefaults()
        {
            EnsureFolder(TrackFolder);

            CreateTrack("Weapon_Magazine", AugmentCategory.Weapon, "탄창 & 재장전",
                "탄창 용량 +20%, 재장전 속도 +10%",
                "탄창 용량 +35%, 재장전 속도 +20%",
                "탄창 용량 +50%, 재장전 속도 +35%");

            CreateTrack("Weapon_Burst", AugmentCategory.Weapon, "점사 강화",
                "점사 총알 수 +1, 발사 대기시간 -10%",
                "점사 총알 수 +2, 발사 대기시간 -20%",
                "점사 총알 수 +3, 발사 대기시간 -35%");

            CreateTrack("Q_Knockback", AugmentCategory.Q, "넉백/기절 강화",
                "넉백",
                "넉백 + 기절 추가",
                "넉백 사거리 증가 + 넉백 중 주변 적 접촉 시 함께 넉백/기절");

            CreateTrack("Q_Dash", AugmentCategory.Q, "대쉬 강화",
                "대쉬 이후 이동속도 +10%",
                "대쉬 중 무적 + 이동속도 +30%",
                "대쉬 이후 0.15초 무적 + 이동속도 +50%");

            CreateTrack("E_Bleed", AugmentCategory.E, "출혈 강화",
                "출혈 지속시간 증가",
                "출혈 지속시간 증가 + 슬로우 추가",
                "출혈 중인 대상 공격 시 피 회복");

            CreateTrack("E_Slash", AugmentCategory.E, "베기 강화",
                "지속시간 내 3회 사용 가능",
                "베기 범위 증가",
                "넉백 거리 증가 + 벽에 부딪히면 추가 데미지/기절 (벽꿍)");

            CreateTrack("Passive_Kill", AugmentCategory.Passive, "처치 강화",
                "처치 시 버프 지속시간 증가",
                "처치 시 기본 스킬 쿨타임 감소 추가",
                "피격 시에도 효과 적용 (효과는 점차 감소)");

            CreateTrack("Passive_Headshot", AugmentCategory.Passive, "헤드샷 강화",
                "뒤통수(백어택) 시 헤드샷 배율 n배 적용",
                "헤드샷 추가 데미지",
                "피격 시 모든 부위가 헤드 판정으로 적용");

            CreateChannelIfMissing();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AugmentTrackSeeder] 기본 증강 트랙 8종 + 이벤트 채널 생성/갱신 완료");
        }

        private static void CreateTrack(string trackId, AugmentCategory category, string trackName, string tier1, string tier2, string tier3)
        {
            string path = $"{TrackFolder}/{trackId}.asset";
            AugmentTrackSO track = AssetDatabase.LoadAssetAtPath<AugmentTrackSO>(path);
            if (track == null)
            {
                track = ScriptableObject.CreateInstance<AugmentTrackSO>();
                AssetDatabase.CreateAsset(track, path);
            }

            track.trackId = trackId;
            track.category = category;
            track.trackName = trackName;
            track.tiers = new[]
            {
                new AugmentTierInfo { description = tier1 },
                new AugmentTierInfo { description = tier2 },
                new AugmentTierInfo { description = tier3 },
            };

            EditorUtility.SetDirty(track);
        }

        private static void CreateChannelIfMissing()
        {
            EventChannelSO existing = AssetDatabase.LoadAssetAtPath<EventChannelSO>(ChannelPath);
            if (existing == null)
            {
                EventChannelSO channel = ScriptableObject.CreateInstance<EventChannelSO>();
                AssetDatabase.CreateAsset(channel, ChannelPath);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
