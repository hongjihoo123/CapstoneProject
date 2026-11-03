using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    public class PlayGraphVfx : MonoBehaviour, IPlayableVFX
    {
        [field: SerializeField] public AnimParamSO VfxName { get; private set; }
        [field: SerializeField] public float VfxDuration { get; private set; }
        [SerializeField] private VisualEffect[] effects;

        private Coroutine _stopCoroutine;

        public void PlayVfx(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            PlayVfx();
        }

        public void PlayVfx()
        {
            if (_stopCoroutine != null)
                StopCoroutine(_stopCoroutine);

            foreach (VisualEffect effect in effects)
            {
                effect.Play();
            }

            _stopCoroutine = StartCoroutine(StopAfterDuration());
        }

        public void StopVfx()
        {
            if (_stopCoroutine != null)
            {
                StopCoroutine(_stopCoroutine);
                _stopCoroutine = null;
            }

            foreach (VisualEffect effect in effects)
            {
                effect.Stop();
            }
        }

        private IEnumerator StopAfterDuration()
        {
            yield return new WaitForSeconds(VfxDuration);
            _stopCoroutine = null;
            StopVfx();
        }
    }
}