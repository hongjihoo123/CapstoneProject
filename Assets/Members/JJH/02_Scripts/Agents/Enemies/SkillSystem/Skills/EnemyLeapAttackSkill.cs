
using Members.JJH._02_Scripts.Agents.Enemies.BT;
using RobotWeapons;
using System.Collections;
using Unity.Behavior;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyLeapAttackSkill : AbstractEnemySkill
    {
        [Header("Leap Settings")]
        [SerializeField] private float leapHeight = 3f;
        [SerializeField] private float leapDamageRange = 2f;
        [Tooltip("상승 시간 비율"), SerializeField, Min(0.01f)] private float riseRatio = 0.35f;
        [Tooltip("멈추는 시간 비율"), SerializeField, Min(0f)] private float hangRatio = 0.2f;
        [Tooltip("하강 시간 비율"), SerializeField, Min(0.01f)] private float fallRatio = 0.45f;

        protected override IEnumerator Execute()
        {
            _skillModule.enemy.GetBTVariable(BTVars.Target, out BlackboardVariable<GameObject> target);
            if (target == null || target.Value == null)
                yield break;

            float prepare = SkillSO.PrepareTime;
            float duration = SkillSO.AttackDutation;

            Vector3 start = _enemy.transform.position;
            Vector3 landing = target.Value.transform.position;
            landing.y = start.y;
            Vector3 peak = landing + Vector3.up * leapHeight;

            float sum = riseRatio + hangRatio + fallRatio;
            float riseTime = duration * riseRatio / sum;
            float hangTime = duration * hangRatio / sum;
            float fallTime = duration * fallRatio / sum;

            _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                landing, leapDamageRange, prepare + riseTime + hangTime + fallTime));

            yield return new WaitForSeconds(prepare);

            var agent = _skillModule.enemy.NavMeshAgent.NavMeshAgent;
            agent.enabled = false;

            yield return Tick(riseTime, t =>
            {
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                _enemy.transform.position = Vector3.Lerp(start, peak, eased);
            });
            _enemy.transform.position = peak;

            Coroutine warning = StartCoroutine(ExpandCircleWarning(landing, leapDamageRange, hangTime + fallTime));

            yield return new WaitForSeconds(hangTime);
            yield return Tick(fallTime, t =>
                _enemy.transform.position = Vector3.Lerp(peak, landing, t * t * t));

            _enemy.transform.position = landing;
            agent.enabled = true;
            agent.Warp(landing);

            PlaySkillVfx();
            DamageInCircle(landing, leapDamageRange, ignoreSelf: true);
        }

        protected override void OnCancelled()
        {
            var agent = _skillModule.enemy.NavMeshAgent.NavMeshAgent;
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh == false)
                    agent.Warp(_enemy.transform.position);
            }
        }

#if UNITY_EDITOR

        protected override void DrawRangeGizmo()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            DrawGizmoCircle(transform.position, leapDamageRange);

            Gizmos.color = new Color(1f, 1f, 0.2f, 0.9f);
            Vector3 peak = transform.position + Vector3.up * leapHeight;
            Gizmos.DrawLine(transform.position, peak);
            Gizmos.DrawWireSphere(peak, 0.2f);

            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.4f);
            DrawGizmoCircle(transform.position, SkillSO.Range);
        }

#endif
    }
}
