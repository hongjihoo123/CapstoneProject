using RobotWeapons;
using System;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills
{
    public abstract class AbstractEnemySkill : MonoBehaviour, IEnemySkill
    {
        [field: SerializeField] public EnemySkillSO SkillSO { get; private set; }

        [Header("Warning Visual")]
        [SerializeField, Min(0.01f)] private float warningTickInterval = 0.05f;

        public bool IsUsing { get; protected set; }

        protected AbstractEnemy _enemy;
        protected IEnemySkillModule _skillModule;

        private Coroutine _skillCoroutine;

        public virtual void Initialize(AbstractEnemy owner, EnemySkillModule skillModule)
        {
            _enemy = owner;
            _skillModule = skillModule;
        }

        public void UseSkill()
        {
            if (IsUsing || _enemy == null || SkillSO == null)
                return;

            _skillCoroutine = StartCoroutine(RunSkill());
        }

        protected abstract IEnumerator Execute();

        private IEnumerator RunSkill()
        {
            IsUsing = true;
            try
            {
                yield return Execute();
            }
            finally
            {
                IsUsing = false;
                _skillCoroutine = null;
            }
        }

        protected virtual void OnDisable()
        {
            if (_skillCoroutine != null)
            {
                StopCoroutine(_skillCoroutine);
                _skillCoroutine = null;
            }
            IsUsing = false;
        }

        protected static IEnumerator Tick(float duration, Action<float> onTick)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                onTick(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }

        #region Expand Warning

        protected IEnumerator ExpandWarning(Func<float, float, AttackArea> factory, float duration)
        {
            float life = warningTickInterval + 0.02f;
            float elapsed = 0f;
            float sinceShow = warningTickInterval;

            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                sinceShow += dt;

                if (sinceShow >= warningTickInterval)
                {
                    sinceShow = 0f;
                    _skillModule.AreaVisualizer.Show(factory(Mathf.Clamp01(elapsed / duration), life));
                }
                yield return null;
            }
        }

        protected IEnumerator ExpandCircleWarning(Vector3 center, float maxRadius,
                                                                                float duration, float startRadius = 0.1f)
        {
            return ExpandWarning((t, life) => AttackArea.Circle(AttackAreaKind.EnemyWarning,
                center, Mathf.Lerp(startRadius, maxRadius, t), life), duration);
        }

        protected IEnumerator ExpandConeWarning(Vector3 center, Quaternion rotation, float maxRange,
                                                float angle, float duration, float startRange = 0.1f)
        {
            return ExpandWarning((t, life) => AttackArea.Cone(AttackAreaKind.EnemyWarning,
                center, rotation, Mathf.Lerp(startRange, maxRange, t), angle, life), duration);
        }

        #endregion

        #region Skill Gizmo (Editor)

#if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            if (SkillSO == null)
                return;

            DrawRangeGizmo();
        }

        protected virtual void DrawRangeGizmo()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            DrawGizmoCircle(transform.position, SkillSO.Range);
        }

        protected static void DrawGizmoCircle(Vector3 center, float radius, int segments = 48)
        {
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float rad = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        protected static void DrawGizmoCone(Vector3 center, Quaternion rotation, float range, float angle, int segments = 24)
        {
            float half = angle * 0.5f;
            Vector3 prev = center + rotation * Quaternion.Euler(0f, -half, 0f) * Vector3.forward * range;

            Gizmos.DrawLine(center, prev);
            for (int i = 1; i <= segments; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)segments);
                Vector3 next = center + rotation * Quaternion.Euler(0f, a, 0f) * Vector3.forward * range;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
            Gizmos.DrawLine(prev, center);
        }

        protected static void DrawGizmoBox(Vector3 center, Quaternion rotation, Vector3 size)
        {
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = old;
        }

#endif

        #endregion

        #region Damage

        protected void DamageInCircle(Vector3 center, float range, bool ignoreSelf = false)
        {
            Collider[] hits = Physics.OverlapSphere(center, range, _skillModule.TargetLayer);
            foreach (Collider hit in hits)
            {
                if (ignoreSelf && hit.transform.IsChildOf(_enemy.transform))
                    continue;

                Vector3 dir = hit.transform.position - center;
                dir.y = 0f;
                if (dir.sqrMagnitude > range * range)
                    continue;

                ApplyDamage(hit);
            }
        }

        protected void DamageInCone(Vector3 center, Quaternion rotation, float range, float angle)
        {
            Vector3 forward = rotation * Vector3.forward;
            Collider[] hits = Physics.OverlapSphere(center, range, _skillModule.TargetLayer);
            foreach (Collider hit in hits)
            {
                Vector3 dir = hit.transform.position - center;
                dir.y = 0f;

                if (dir.sqrMagnitude > range * range ||
                    Vector3.Angle(forward, dir) > angle * 0.5f)
                    continue;

                ApplyDamage(hit);
            }
        }

        protected void ApplyDamage(Collider hit)
        {
            if (!hit.TryGetComponent<IDamageable>(out var damageable))
                damageable = hit.GetComponentInParent<IDamageable>();

            if (damageable == null || !damageable.IsAlive)
                return;

            damageable.TakeDamage(SkillSO.Damage, _enemy.gameObject);
        }

        #endregion
    }
}