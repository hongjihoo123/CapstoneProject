using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.KYR._01_Scripts;

namespace Members.JJH._02_Scripts.Augments
{
    // 특정 플레이어에게 증강 선택 UI를 띄워달라는 요청 이벤트.
    // Player가 null이면 "로컬 플레이어"를 의미한다 (AugmentManager가 알아서 찾음).
    public class AugmentSelectionRequestedEvent : GameEvent
    {
        public readonly PlayerAgent Player;

        public AugmentSelectionRequestedEvent(PlayerAgent player = null)
        {
            Player = player;
        }
    }

    // 플레이어가 증강 하나를 선택해서 해당 트랙의 단계가 올라갔을 때 발생하는 이벤트.
    // 실제 게임 효과(스탯 증가 등)는 각 시스템이 이 이벤트를 구독해서 그 Player에게만 직접 적용하면 됨.
    public class AugmentAppliedEvent : GameEvent
    {
        public readonly PlayerAgent Player;
        public readonly AugmentTrackSO Track;
        public readonly int Tier;

        public AugmentAppliedEvent(PlayerAgent player, AugmentTrackSO track, int tier)
        {
            Player = player;
            Track = track;
            Tier = tier;
        }
    }

    // 기지로 복귀해서 재정비할 때 발생 - 그 플레이어의 증강을 전부 초기화하라는 신호.
    public class AugmentsResetEvent : GameEvent
    {
        public readonly PlayerAgent Player;

        public AugmentsResetEvent(PlayerAgent player)
        {
            Player = player;
        }
    }
}
