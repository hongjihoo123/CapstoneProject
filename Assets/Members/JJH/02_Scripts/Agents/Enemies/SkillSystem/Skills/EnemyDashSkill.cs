using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyDashSkill : MonoBehaviour, IEnemySkill
    {
        public bool IsUsing { get; private set; }

        [SerializeField] private float prepareTime = 1f;
        [SerializeField] private float dashDistance = 10f;
        [SerializeField] private float dashDuration = 0.2f;

        private AbstractEnemy _enemy;
        private EnemySkillModule _skillModule;
        private Coroutine _dashCoroutine;

        public void Initialize(AbstractEnemy owner, EnemySkillModule skillModule)
        {
            _enemy = owner;
            _skillModule = skillModule;
        }

        public void UseSkill()
        {
            if (IsUsing || _enemy == null)
                return;

            _dashCoroutine = StartCoroutine(UseSkillCoroutine());
        }

        private IEnumerator UseSkillCoroutine()
        {
            IsUsing = true;

            _skillModule.AreaVisualizer.Show(
                            AttackArea.Box(AttackAreaKind.EnemyWarning,
                                                     _enemy.transform.position + _enemy.transform.forward * (dashDistance * 0.5f),
                                                     _enemy.transform.rotation, new Vector3(2f, 0f, dashDistance),
                                                     prepareTime));

            yield return new WaitForSeconds(prepareTime);

            Vector3 startPosition = _enemy.transform.position;
            Vector3 direction = _enemy.transform.forward;
            direction.y = 0f;
            direction.Normalize();

            Vector3 targetPosition = startPosition + direction * dashDistance;
            float elapsed = 0f;

            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / dashDuration);
                _enemy.transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
                yield return null;
            }

            _enemy.transform.position = targetPosition;
            IsUsing = false;
            _dashCoroutine = null;
        }
    }
}