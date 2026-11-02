using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using UnityEngine;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    public class PlayParticleVfx : MonoBehaviour, IPlayableVFX
    {
        [field: SerializeField] public AnimParamSO VfxName { get; private set; }
        [field: SerializeField] public float VfxDuration { get; private set; }
        [SerializeField] private ParticleSystem[] particles;

        public void PlayVfx(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            PlayVfx();
        }

        public void PlayVfx()
        {
            foreach (ParticleSystem ps in particles)
            {
                ps.Play(true);
            }
        }

        public void StopVfx()
        {
            foreach (ParticleSystem ps in particles)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}