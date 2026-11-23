using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Quest;
using Members.JJH._02_Scripts.Agents.Enemies;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    // 실전: 튜토리얼 동안 멈춰 둔 맵 스포너·씬 적을 풀고, 방의 적을 모두 잡으면 끝.
    // 화면 밖 적/아직 안 가 본 스포너 쪽으로 가장자리 화살표로 길 안내.
    //
    // 끝 판단: RoomClearTracker.IsCleared. 단, 죽음 연출에 Dead() 신호가 없는 적(현재 엔트 보스: 죽음 애니메이션에
    // OnObjectDead 이벤트 없음)은 체력 0 이 되어도 방 클리어 수에 안 잡혀서, 체력 0 으로 잠깐 서 있으면 처치로 쳐 줌.
    [CreateAssetMenu(menuName = "HJH/Tutorial/Step - Clear Room", fileName = "Step_ClearRoom")]
    public class ClearRoomStep : TutorialStep
    {
        private const float StuckDeadSeconds = 1.5f;

        [System.NonSerialized] private float _nextScan;
        [System.NonSerialized] private readonly Dictionary<AbstractEnemy, float> _zeroSince = new();
        [System.NonSerialized] private int _stuckDead;

        public override void Begin(TutorialContext ctx)
        {
            ctx.HoldStageSpawners(false);
            _nextScan = 0f;
            _zeroSince.Clear();
            _stuckDead = 0;
        }

        public override bool IsDone(TutorialContext ctx)
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.25f;
                ctx.View.SetGuidePoint(ctx.TryGetGuideTarget(out Vector3 point) ? point : (Vector3?)null);
                ScanStuckDead();
            }

            // 방 클리어 추적이 없는 씬(적이 없는 방)이면 바로 끝
            if (ctx.Tracker == null || ctx.Tracker.IsCleared || ctx.Tracker.Total <= 0)
                return true;
            return Left(ctx) <= 0 && !AnyAlive();
        }

        private int Left(TutorialContext ctx) => Mathf.Max(0, ctx.Tracker.Total - ctx.Tracker.Kills - _stuckDead);

        // 체력 0 인데 Dead() 가 안 불려 남아 있는 적을 셈
        private void ScanStuckDead()
        {
            int stuck = 0;
            foreach (AbstractEnemy enemy in Object.FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
            {
                if (enemy == null || enemy.IsAlive || enemy.GetComponentInParent<RoomClearIgnore>() != null)
                    continue;
                if (!_zeroSince.TryGetValue(enemy, out float since))
                    _zeroSince[enemy] = since = Time.unscaledTime;
                if (Time.unscaledTime - since >= StuckDeadSeconds)
                    stuck++;
            }
            _stuckDead = stuck;
        }

        private static bool AnyAlive()
        {
            foreach (AbstractEnemy enemy in Object.FindObjectsByType<AbstractEnemy>(FindObjectsSortMode.None))
            {
                if (enemy != null && enemy.IsAlive && enemy.GetComponentInParent<RoomClearIgnore>() == null)
                    return true;
            }
            return false;
        }

        public override string Progress(TutorialContext ctx)
        {
            if (ctx.Tracker == null)
                return null;
            int left = Left(ctx);
            return left > 0 ? $"남은 적 {left}마리" : "<color=#D6B878>방 클리어!</color>";
        }

        public override void End(TutorialContext ctx) => ctx.View.SetGuidePoint(null);
    }
}
