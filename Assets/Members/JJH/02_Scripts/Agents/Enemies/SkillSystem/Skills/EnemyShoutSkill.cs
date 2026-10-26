using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyShoutSkill : AbstractEnemySkill
    {
        [Header("Shout Settings")]
        [SerializeField] private float angle = 45f;

        private Coroutine _skillCoroutine;

        public override void UseSkill()
        {
            base.UseSkill();

            _skillCoroutine = StartCoroutine(UseSkillCoroutine());
        }

        private IEnumerator UseSkillCoroutine()
        {
            IsUsing = true;

            Vector3 center = _enemy.transform.position;
            Quaternion rotation = _enemy.transform.rotation;
            float prepareTime = SkillSO.PrepareTime;
            float expandTime = SkillSO.ExpandTime;
            float skillRange = SkillSO.Range;

            _skillModule.AreaVisualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning,
                                                          center, rotation, skillRange, angle, prepareTime + expandTime));

            yield return new WaitForSeconds(prepareTime);

            float elapsed = 0f;
            float startRange = 0.1f;

            while (elapsed < expandTime)
            {
                elapsed += Time.deltaTime;
                float currentRange = Mathf.Lerp(startRange, skillRange,
                                                            Mathf.Clamp01(elapsed / expandTime));

                _skillModule.AreaVisualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning,
                                                                            center, rotation, currentRange, angle, Time.deltaTime * 2f));

                yield return null;
            }

            Collider[] hits = Physics.OverlapSphere(center, skillRange);
            foreach (Collider hit in hits)
            {
                Vector3 direction = hit.transform.position - center;
                direction.y = 0f;

                if (direction.sqrMagnitude > skillRange * skillRange ||
                    Vector3.Angle(rotation * Vector3.forward, direction) > angle * 0.5f)
                    continue;
            }

            yield return new WaitForSeconds(SkillSO.AttackDutation);

            IsUsing = false;
            _skillCoroutine = null;
        }
    }
}