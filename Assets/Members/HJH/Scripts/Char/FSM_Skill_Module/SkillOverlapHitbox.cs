using System.Collections.Generic;
using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    public class SkillOverlapHitbox : MonoBehaviour
    {
        [SerializeField] private Vector3 boxSize = new Vector3(1f, 1f, 1.5f);
        [SerializeField] private Vector3 boxCenterOffset = Vector3.zero;
        [SerializeField] private LayerMask hitMask = ~0;

        public List<IDamageable> Overlap()
        {
            var results = new List<IDamageable>();
            Vector3 worldCenter = transform.TransformPoint(boxCenterOffset);
            Collider[] hits = Physics.OverlapBox(worldCenter, boxSize * 0.5f, transform.rotation, hitMask, QueryTriggerInteraction.Collide);

            foreach (var col in hits)
            {
                var target = col.GetComponentInParent<IDamageable>();
                if (target != null && !results.Contains(target))
                    results.Add(target);
            }
            return results;
        }

        private void OnDrawGizmos()
        {
            Vector3 worldCenter = transform.TransformPoint(boxCenterOffset);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(worldCenter, transform.rotation, Vector3.one);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawCube(Vector3.zero, boxSize);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Vector3.zero, boxSize);

            Gizmos.matrix = oldMatrix;
        }
    }
}