using UnityEngine;

namespace RobotWeapons
{
    public class MuzzleFlash : MonoBehaviour
    {
        [SerializeField] private ParticleSystem flashParticle;
        [SerializeField] private Light flashLight;
        [SerializeField] private float lightDuration = 0.05f;

        private float lightTimer;

        public void Play()
        {
            flashParticle?.Play();
            if (flashLight != null)
            {
                flashLight.enabled = true;
                lightTimer = lightDuration;
            }
        }

        private void Update()
        {
            if (lightTimer <= 0f) return;
            lightTimer -= Time.deltaTime;
            if (lightTimer <= 0f && flashLight != null)
                flashLight.enabled = false;
        }
    }
}
