using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RobotWeapons
{
    public class HealScreenEffect : MonoBehaviour
    {
        [SerializeField] private float pulseDuration = 0.6f;
        [SerializeField] private float maxVignetteIntensity = 0.35f;
        [SerializeField] private Color healColor = new Color(0.4f, 1f, 0.5f);

        [SerializeField]private Volume volume;
        private Vignette vignette;
        private Coroutine activePulse;

        private void Awake()
        {
            volume.profile.TryGet(out vignette);
        }

        public void Pulse()
        {
            if (vignette == null) return;
            if (activePulse != null) StopCoroutine(activePulse);
            activePulse = StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            vignette.color.value = healColor;
            float half = pulseDuration * 0.5f;

            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                vignette.intensity.value = Mathf.Lerp(0f, maxVignetteIntensity, t / half);
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                vignette.intensity.value = Mathf.Lerp(maxVignetteIntensity, 0f, t / half);
                yield return null;
            }

            vignette.intensity.value = 0f;
        }
    }
}
