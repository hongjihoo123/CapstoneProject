using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Emission rate x Value (e.g. aura denser with more stacks).
    public class FxEmissionByValue : MonoBehaviour, IFxDriver, IFxValueReceiver
    {
        [SerializeField] private ParticleSystem[] systems;
        [SerializeField] private float ratePerValue = 8f;

        public bool IsPlaying => false;

        public void Play(in FxParams parameters) => SetValue(parameters.Value);

        public void SetValue(float value)
        {
            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = ratePerValue * Mathf.Max(0f, value);
            }
        }

        public void Stop() { }
    }
}
