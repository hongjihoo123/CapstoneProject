using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Quest;
using Members.JJH._02_Scripts.Agents.Enemies;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
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

            if (ctx.Tracker == null || ctx.Tracker.IsCleared || ctx.Tracker.Total <= 0)
                return true;
            return Left(ctx) <= 0 && !AnyAlive();
        }

        private int Left(TutorialContext ctx) => Mathf.Max(0, ctx.Tracker.Total - ctx.Tracker.Kills - _stuckDead);

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
