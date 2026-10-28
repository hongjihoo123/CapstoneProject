using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyWideSweepSkill : AbstractEnemySkill
    {
        [Header("Wide Sweep Settings")]
        [SerializeField] private float angle = 120f;

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
            float skillRange = SkillSO.Range;

            _skillModule.AreaVisualizer.Show(AttackArea.Cone(AttackAreaKind.EnemyWarning,
                   center, rotation, skillRange, angle, prepareTime));

            yield return new WaitForSeconds(prepareTime);

            Collider[] hits = Physics.OverlapSphere(center, skillRange, _skillModule.TargetLayer);
            foreach (Collider hit in hits)
            {
                Vector3 direction = hit.transform.position - center;
                direction.y = 0f;

                if (direction.sqrMagnitude > skillRange * skillRange ||
                    Vector3.Angle(rotation * Vector3.forward, direction) > angle * 0.5f)
                    continue;

                ApplyDamage(hit);
            }

            yield return new WaitForSeconds(SkillSO.AttackDutation);

            IsUsing = false;
            _skillCoroutine = null;
        }
    }
}