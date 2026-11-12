using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Enemies.SkillSystem.Skills.DamageProjectiles
{
    public class BossThornProjectile : MonoBehaviour
    {
        [Header("Motion")]
        [SerializeField] private float hiddenDepth = 3f;
        [SerializeField] private float riseTime = 0.15f;
        [SerializeField] private float holdTime = 0.6f;
        [SerializeField] private float sinkTime = 0.4f;

        public void Play(Vector3 groundPosition)
        {
            StartCoroutine(PlayRoutine(groundPosition));
        }

        private IEnumerator PlayRoutine(Vector3 groundPosition)
        {
            Vector3 hidden = groundPosition + Vector3.down * hiddenDepth;
            transform.position = hidden;

            yield return Move(hidden, groundPosition, riseTime, true);
            yield return new WaitForSeconds(holdTime);
            yield return Move(groundPosition, hidden, sinkTime, false);

            Destroy(gameObject);
        }

        private IEnumerator Move(Vector3 from, Vector3 to, float duration, bool fast)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float eased = fast ? 1f - (1f - t) * (1f - t) : t * t;
                transform.position = Vector3.Lerp(from, to, eased);
                yield return null;
            }
            transform.position = to;
        }
    }
}