using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyShoutSkill : AbstractEnemySkill
    {
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

            _skillModule.AreaVisualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning,
                                                                        center, rotation, SkillSO.SkillRange,
                                                                        angle, SkillSO.SkillPrepareTime + SkillSO.SkillExpandTime));

            yield return new WaitForSeconds(SkillSO.SkillPrepareTime);

            float elapsed = 0f;
            float startRange = 0.1f;

            while (elapsed < SkillSO.SkillExpandTime)
            {
                elapsed += Time.deltaTime;
                float currentRange = Mathf.Lerp(startRange, SkillSO.SkillRange,
                                                            Mathf.Clamp01(elapsed / SkillSO.SkillExpandTime));

                _skillModule.AreaVisualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning,
                                                                            center, rotation, currentRange, angle, Time.deltaTime * 2f));

                yield return null;
            }

            Collider[] hits = Physics.OverlapSphere(center, SkillSO.SkillRange);

            foreach (Collider hit in hits)
            {
                Vector3 direction = hit.transform.position - center;
                direction.y = 0f;

                if (direction.sqrMagnitude > SkillSO.SkillRange * SkillSO.SkillRange ||
                    Vector3.Angle(rotation * Vector3.forward, direction) > angle * 0.5f)
                    continue;
            }

            IsUsing = false;
            _skillCoroutine = null;
        }
    }
}