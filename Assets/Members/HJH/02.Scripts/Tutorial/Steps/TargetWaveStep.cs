using System.Collections.Generic;
using Members.JJH._02_Scripts.Agents.Enemies;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    [CreateAssetMenu(menuName = "HJH/Tutorial/Step - Target Wave", fileName = "Step_TargetWave")]
    public class TargetWaveStep : TutorialStep
    {
        [SerializeField] private GameObject targetPrefab;
        [SerializeField, Min(1), Tooltip("자리가 없을 때 세우는 수")] private int fallbackCount = 4;
        [SerializeField, Min(2f)] private float fallbackRadius = 6f;

        [System.NonSerialized] private readonly List<AbstractEnemy> _targets = new();

        public void SetTarget(GameObject prefab) => targetPrefab = prefab;

        public override void Begin(TutorialContext ctx)
        {
            _targets.Clear();
            ctx.RefreshCooldowns();

            TrainingDummySpot[] spots = Object.FindObjectsByType<TrainingDummySpot>(FindObjectsSortMode.None);
            if (spots.Length > 0)
            {
                foreach (TrainingDummySpot spot in spots)
                    Add(ctx.SpawnTargetAt(targetPrefab, spot.transform.position));
                return;
            }

            if (ctx.Player == null)
                return;
            for (int i = 0; i < fallbackCount; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 360f / fallbackCount, 0f) * Vector3.forward * fallbackRadius;
                Add(ctx.SpawnTargetAt(targetPrefab, ctx.Player.transform.position + offset));
            }
        }

        private void Add(AbstractEnemy enemy)
        {
            if (enemy != null)
                _targets.Add(enemy);
        }

        public override bool IsDone(TutorialContext ctx)
        {
            Vector3? nearest = null;
            float best = float.MaxValue;
            Vector3 me = ctx.Player != null ? ctx.Player.transform.position : Vector3.zero;
            foreach (AbstractEnemy enemy in _targets)
            {
                if (!Alive(enemy))
                    continue;
                float d = (enemy.transform.position - me).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = enemy.transform.position;
                }
            }

            ctx.View.SetGuidePoint(nearest);
            return nearest == null;
        }

        private static bool Alive(AbstractEnemy enemy) => enemy != null && enemy.IsAlive;

        public override string Progress(TutorialContext ctx)
        {
            int left = 0;
            foreach (AbstractEnemy enemy in _targets)
                if (Alive(enemy))
                    left++;
            return left > 0 ? $"남은 더미 {left} / {_targets.Count}" : "<color=#D6B878>훈련 성공!</color>";
        }

        public override void End(TutorialContext ctx)
        {
            ctx.View.SetGuidePoint(null);
            foreach (AbstractEnemy enemy in _targets)
                if (Alive(enemy))
                    Destroy(enemy.gameObject);
            _targets.Clear();
        }
    }
}
