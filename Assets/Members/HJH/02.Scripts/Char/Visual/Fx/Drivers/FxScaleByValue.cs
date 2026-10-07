using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Scales a child between two sizes by Value 0..1 (charge-up growing).
    public class FxScaleByValue : MonoBehaviour, IFxDriver, IFxValueReceiver
    {
        [SerializeField] private Transform target;
        [SerializeField] private float minScale = 0.4f;
        [SerializeField] private float maxScale = 1.6f;

        public bool IsPlaying => false;

        public void Play(in FxParams parameters) => SetValue(parameters.Value);

        public void SetValue(float value)
        {
            if (target != null)
                target.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, Mathf.Clamp01(value));
        }

        public void Stop() { }
    }
}
