using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 폭포가 떨어지는 수면에 물결을 계속 만들어 줌 (프로젝트의 물결 시뮬레이션 JungleWater.Ripple 사용).
    // 이 오브젝트의 위치 = 폭포 바닥 중앙, right 축 = 폭포 폭 방향. 플레이 중에만 동작.
    public class WaterfallRipples : MonoBehaviour
    {
        [SerializeField, Tooltip("폭포 폭의 절반 (m)")] private float halfWidth = 2f;
        [SerializeField, Tooltip("물결 하나 만드는 간격(초). 작을수록 많이 침")] private float interval = 0.12f;
        [SerializeField, Tooltip("물결 반지름 (m)")] private Vector2 radius = new Vector2(0.7f, 1.3f);
        [SerializeField, Tooltip("물결 세기")] private Vector2 strength = new Vector2(0.08f, 0.2f);

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
