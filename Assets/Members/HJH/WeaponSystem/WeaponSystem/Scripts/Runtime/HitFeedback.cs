using UnityEngine;
using UnityEngine.UI;

namespace RobotWeapons
{
    // 명중 피드백(히트마커+사운드). owner.ApplyDamageTo() 한 곳에서만 호출하면
    // 총(발당 한 번)이든 레이저(연속 틱)든 똑같이 커버됨.
    // minInterval로 연속 타격 시 과도한 반복을 막아 "두구두구" 리듬감을 만듦.
    // isWeakpoint에 따라 마커 색/사운드가 갈림.
    public class HitFeedback : MonoBehaviour
    {
        [SerializeField] private Image hitMarker;
        [SerializeField] private float markerDuration = 0.1f;
        [SerializeField] private float minInterval = 0.1f;

        [Header("일반 타격")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private AudioClip normalHitSound;

        [Header("약점 타격")]
        [SerializeField] private Color weakpointColor = Color.red;
        [SerializeField] private AudioClip weakpointHitSound;

        [SerializeField] private AudioSource audioSource;

        private float lastPlayTime = -999f;
        private float markerTimer;

        public void Play(bool isWeakpoint = false)
        {
            if (Time.time - lastPlayTime < minInterval) return;
            lastPlayTime = Time.time;

            if (hitMarker != null)
            {
                hitMarker.color = isWeakpoint ? weakpointColor : normalColor;
                hitMarker.gameObject.SetActive(true);
                markerTimer = markerDuration;
            }

            AudioClip clip = isWeakpoint ? weakpointHitSound : normalHitSound;
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        private void Update()
        {
            if (markerTimer <= 0f) return;
            markerTimer -= Time.deltaTime;
            if (markerTimer <= 0f && hitMarker != null)
                hitMarker.gameObject.SetActive(false);
        }
    }
}
