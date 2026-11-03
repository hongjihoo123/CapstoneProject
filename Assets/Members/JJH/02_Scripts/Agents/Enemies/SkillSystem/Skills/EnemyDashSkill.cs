using RobotWeapons;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public class EnemyDashSkill : AbstractEnemySkill
    {
        private const float ExtraDistance = 1.5f;

        protected override IEnumerator Execute()
        {
            float prepare = SkillSO.PrepareTime;
            float expand = SkillSO.ExpandTime;
            float range = SkillSO.Range;
            float length = range + ExtraDistance;

            _skillModule.AreaVisualizer.Show(AttackArea.Box(AttackAreaKind.EnemyWarning,
                _enemy.transform.position + _enemy.transform.forward * (range * 0.5f),
                _enemy.transform.rotation, new Vector3(2f, 0f, length), prepare));

            yield return new WaitForSeconds(prepare);

            Vector3 start = _enemy.transform.position;
            Vector3 dir = _enemy.transform.forward;
            dir.y = 0f;
            dir.Normalize();
            Vector3 target = start + dir * length;

            yield return Tick(expand, t => _enemy.transform.position = Vector3.Lerp(start, target, t));
            _enemy.transform.position = target;

            Collider[] hits = Physics.OverlapBox((start + target) * 0.5f,
                new Vector3(1f, 1f, Vector3.Distance(start, target) * 0.5f),
                _enemy.transform.rotation, _skillModule.TargetLayer);
            foreach (Collider hit in hits)
                ApplyDamage(hit);
        }
    }
}