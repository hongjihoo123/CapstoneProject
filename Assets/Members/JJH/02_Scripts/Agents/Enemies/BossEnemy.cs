using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies
{
    public class BossEnemy : AbstractEnemy
    {
        [Header("Boss Settings")]
        [SerializeField, Range(0f, 1f)] private float phase2HealthRatio = 0.5f;

        public BossPhase Phase { get; private set; } = BossPhase.Phase1;

        private void Update()
        {
            if (Phase != BossPhase.Phase1)
                return;

            if (Health.CurrentHealth <= EnemyData.Health * phase2HealthRatio)
            {
                Phase = BossPhase.Transition;
                SkillModule.CancelAllSkills();
            }
        }

        public void CompletePhaseTransition()
        {
            if (Phase != BossPhase.Transition)
                return;

            Phase = BossPhase.Phase2;
        }
    }
}