using Members.JJH._02_Scripts.Systems.AnimatorSystem;
using UnityEngine;
using UnityEngine.VFX;

namespace Members.JJH._02_Scripts.Agents.Effects
{
    public class PlayGraphVfx : MonoBehaviour, IPlayableVFX
    {
        [field: SerializeField] public AnimParamSO VfxName { get; private set; }
        [field: SerializeField] public float VfxDuration { get; private set; }
        [SerializeField] private VisualEffect[] effects;

        public void PlayVfx(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            PlayVfx();
        }

        public void PlayVfx()
        {
            foreach (VisualEffect effect in effects)
            {
                effect.Play();
            }
        }

        public void StopVfx()
        {
            foreach (VisualEffect effect in effects)
            {
                effect.Stop();
            }
        }
    }
}