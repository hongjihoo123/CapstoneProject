using RobotWeapons;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyLeapAttackSkill : AbstractEnemySkill
    {
        [Header("Leap Settings")]
        [SerializeField] private float leapHeight = 3f;

        private Coroutine _leapCoroutine;
        private NavMeshAgent _navMeshAgent;

        public override void UseSkill()
        {
            if (IsUsing || _enemy == null)
                return;

            _leapCoroutine = StartCoroutine(LeapAttackCoroutine());
        }

        private IEnumerator LeapAttackCoroutine()
        {
            IsUsing = true;

            float prepareTime = SkillSO.PrepareTime;
            float attackDuration = SkillSO.AttackDutation;
            float skillRange = SkillSO.Range;

            if (_navMeshAgent == null)
                _navMeshAgent = _enemy.GetComponent<NavMeshAgent>();

            bool wasAgentEnabled = _navMeshAgent != null && _navMeshAgent.enabled;

            if (wasAgentEnabled)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.enabled = false;
            }

            Vector3 startPosition = _enemy.transform.position;
            GameObject target = GameObject.FindGameObjectWithTag("Player");

            if (target == null)
            {
                RestoreNavMesh(wasAgentEnabled);
                IsUsing = false;
                yield break;
            }

            Vector3 landingPosition = target.transform.position;
            landingPosition.y = startPosition.y;

            _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning,
                                                                        landingPosition, skillRange, prepareTime));

            yield return new WaitForSeconds(prepareTime);

            float elapsed = 0f;

            while (elapsed < attackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / attackDuration);

                Vector3 position = Vector3.Lerp(startPosition, landingPosition, t);
                position.y = Mathf.Lerp(startPosition.y, landingPosition.y, t) + Mathf.Sin(t * Mathf.PI) * leapHeight;

                _enemy.transform.position = position;

                yield return null;
            }

            _enemy.transform.position = landingPosition;

            Collider[] hits = Physics.OverlapSphere(landingPosition, skillRange);
            foreach (Collider hit in hits)
            {
                if (hit.gameObject == _enemy.gameObject ||
                    hit.transform.IsChildOf(_enemy.transform))
                    continue;

                if (!hit.CompareTag("Player"))
                    continue;
            }

            RestoreNavMesh(wasAgentEnabled);
            IsUsing = false;
            _leapCoroutine = null;
        }

        private void RestoreNavMesh(bool wasAgentEnabled)
        {
            if (_navMeshAgent == null || !wasAgentEnabled)
                return;

            _navMeshAgent.enabled = true;
            _navMeshAgent.Warp(_enemy.transform.position);
            _navMeshAgent.isStopped = false;
        }

        private void OnDisable()
        {
            if (_leapCoroutine != null)
                StopCoroutine(_leapCoroutine);

            if (IsUsing)
            {
                IsUsing = false;
                RestoreNavMesh(_navMeshAgent != null && !_navMeshAgent.enabled);
            }
        }
    }
}