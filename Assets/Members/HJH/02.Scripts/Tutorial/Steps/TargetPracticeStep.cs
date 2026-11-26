using Members.JJH._02_Scripts.Agents.Enemies;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Tutorial
{
    public abstract class TargetPracticeStep : TutorialStep
    {
        [Header("연습용 적")]
        [SerializeField] private bool spawnTarget = true;
        [SerializeField, Tooltip("비우면 적 없이 진행")] private GameObject targetPrefab;
        [SerializeField, Min(2f)] private float targetDistance = 7f;
        [SerializeField, Tooltip("제자리에 서 있게 (행동 트리 끔)")] private bool stationary = true;
        [SerializeField, Tooltip("단계 시작 때 스킬 쿨타임을 비움 (기다리지 않고 바로 연습)")] private bool refreshCooldowns = true;

        [System.NonSerialized] private AbstractEnemy _target;
        [System.NonSerialized] private bool _hadTarget;

        public bool SpawnsTarget => spawnTarget;

        public void SetTarget(GameObject prefab, bool spawn = true)
        {
            targetPrefab = prefab;
            spawnTarget = spawn;
        }

        public override void Begin(TutorialContext ctx)
        {
            _target = null;
            _hadTarget = false;
            if (refreshCooldowns)
                ctx.RefreshCooldowns();
            if (!spawnTarget)
                return;

            _target = ctx.SpawnTarget(targetPrefab, targetDistance, stationary);
            _hadTarget = _target != null;
            if (_hadTarget)
                ctx.View.SetWorldTarget(_target.transform, guide: true);
        }

        protected bool TargetDown => !_hadTarget || _target == null || !_target.IsAlive;

        protected string TargetProgress => _hadTarget ? (TargetDown ? "<color=#D6B878>처치 완료</color>" : "적 처치 0 / 1") : null;

        public override void End(TutorialContext ctx)
        {
            if (_target != null && _target.IsAlive)
                Destroy(_target.gameObject);
            _target = null;
        }
    }
}
