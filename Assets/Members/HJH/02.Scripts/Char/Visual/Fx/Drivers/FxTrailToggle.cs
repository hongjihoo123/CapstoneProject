using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Trail that only draws while Value > 0 (dash trail: on while dashing).
    public class FxTrailToggle : MonoBehaviour, IFxDriver, IFxValueReceiver
    {
        [SerializeField] private TrailRenderer trail;

        public bool IsPlaying => false;

        public void Play(in FxParams parameters) => SetValue(parameters.Value);

        public void SetValue(float value)
        {
            if (trail != null)
                trail.emitting = value > 0f;
        }

        public void Stop() { }
    }
}
