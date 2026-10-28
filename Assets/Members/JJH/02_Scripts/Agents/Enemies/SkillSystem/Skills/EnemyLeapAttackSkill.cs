
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

        private Coroutine _leapCoroutine;

        public override void UseSkill()
        {
            base.UseSkill();

            _leapCoroutine = StartCoroutine(LeapAttackCoroutine());
        }

        private IEnumerator LeapAttackCoroutine()
        {
            IsUsing = true;

            float prepareTime = SkillSO.PrepareTime;
            float attackDuration = SkillSO.AttackDutation;

            BlackboardVariable<GameObject> target;
            _skillModule.enemy.GetBTVariable(BTVars.Target, out target);

            if (target == null || target.Value == null)
            {
                IsUsing = false;
                _leapCoroutine = null;
                yield break;
            }

            Vector3 startPosition = _enemy.transform.position;
            Vector3 landingPosition = target.Value.transform.position;
            landingPosition.y = startPosition.y;
            Vector3 peakPosition = landingPosition + Vector3.up * leapHeight;

            float ratioSum = riseRatio + hangRatio + fallRatio;
            float riseTime = attackDuration * riseRatio / ratioSum;
            float hangTime = attackDuration * hangRatio / ratioSum;
            float fallTime = attackDuration * fallRatio / ratioSum;

            _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning, landingPosition,
                                                                                        leapDamageRange, prepareTime + attackDuration + fallTime));

            yield return new WaitForSeconds(prepareTime);

            _skillModule.enemy.NavMeshAgent.NavMeshAgent.enabled = false;

            float elapsed = 0f;
            while (elapsed < riseTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / riseTime);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                _enemy.transform.position = Vector3.Lerp(startPosition, peakPosition, eased);
                yield return null;
            }
            _enemy.transform.position = peakPosition;

            yield return new WaitForSeconds(hangTime);


            elapsed = 0f;
            float startRadius = 0.1f;
            float expandDuration = hangTime + fallTime;

            while (elapsed < expandDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / expandDuration);

                if (elapsed <= hangTime)
                {
                    _enemy.transform.position = peakPosition;
                }
                else
                {
                    float fallT = Mathf.Clamp01((elapsed - hangTime) / fallTime);
                    float eased = fallT * fallT * fallT;
                    _enemy.transform.position = Vector3.Lerp(peakPosition, landingPosition, eased);
                }

                float radius = Mathf.Lerp(startRadius, leapDamageRange, t);
                _skillModule.AreaVisualizer.Show(AttackArea.Circle(AttackAreaKind.EnemyWarning, landingPosition, radius, Time.deltaTime * 2f));

                yield return null;
            }

            _enemy.transform.position = landingPosition;

            _skillModule.enemy.NavMeshAgent.NavMeshAgent.enabled = true;
            _skillModule.enemy.NavMeshAgent.NavMeshAgent.Warp(landingPosition);

            Collider[] hits = Physics.OverlapSphere(landingPosition, leapDamageRange, _skillModule.TargetLayer);
            foreach (Collider hit in hits)
            {
                if (hit.transform.IsChildOf(_enemy.transform))
                    continue;

                Vector3 direction = hit.transform.position - landingPosition;
                direction.y = 0f;

                if (direction.sqrMagnitude > leapDamageRange * leapDamageRange)
                    continue;

                ApplyDamage(hit);
            }

            IsUsing = false;
            _leapCoroutine = null;
        }
    }
}
