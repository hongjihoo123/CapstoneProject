using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using System.Collections;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    public class PlayParticleVfx : MonoBehaviour, IPlayableVFX
    {
        [field: SerializeField] public AnimParamSO VfxName { get; private set; }
        [field: SerializeField] public float VfxDuration { get; private set; }
        [SerializeField] private ParticleSystem[] particles;

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

            foreach (ParticleSystem ps in particles)
            {
                ps.Play(true);
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

            foreach (ParticleSystem ps in particles)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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