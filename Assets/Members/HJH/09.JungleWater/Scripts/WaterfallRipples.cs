using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    public class WaterfallRipples : MonoBehaviour
    {
        [SerializeField, Tooltip("폭포 폭의 절반 (m)")] private float halfWidth = 2f;
        [SerializeField, Tooltip("물결 하나 만드는 간격(초). 작을수록 많이 침")] private float interval = 0.07f;
        [SerializeField, Tooltip("물결 반지름 (m)")] private Vector2 radius = new Vector2(0.9f, 2.0f);
        [SerializeField, Tooltip("물결 세기")] private Vector2 strength = new Vector2(0.12f, 0.3f);

        private float _timer;

        public void Configure(float half) => halfWidth = Mathf.Max(0.2f, half);

        private void Update()
        {
            if (!Application.isPlaying)
                return;
            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;
            _timer = interval * Random.Range(0.6f, 1.4f);

            Vector3 p = transform.position + transform.right * Random.Range(-halfWidth, halfWidth);
            JungleWater.Ripple(p, Random.Range(radius.x, radius.y), Random.Range(strength.x, strength.y));
        }
    }
}
