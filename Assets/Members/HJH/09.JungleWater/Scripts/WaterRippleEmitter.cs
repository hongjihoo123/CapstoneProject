using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 캐릭터(발 위치 = 이 Transform)에 붙이면 물속을 걸을 때 물결이 생긴다.
    // 가만히 서 있으면 물결을 만들지 않음 → 금방 잔잔해짐 (피드백: 물결이 너무 많고 오래 남음)
    public class WaterRippleEmitter : MonoBehaviour
    {
        [SerializeField] private float radius = 0.45f;
        [Tooltip("걸을 때 물결 세기")]
        [SerializeField] private float stepStrength = 0.12f;
        [Tooltip("이 거리만큼 움직일 때마다 물결 하나")]
        [SerializeField] private float stepSpacing = 0.7f;
        [Tooltip("발이 수면보다 이만큼 위여도 물속으로 친다")]
        [SerializeField] private float submergeTolerance = 0.05f;

        private Vector3 _lastPosition;

        private void OnEnable() => _lastPosition = transform.position;

        private void Update()
        {
            Vector3 position = transform.position;
            if (!JungleWater.IsInWater(position, submergeTolerance))
            {
                _lastPosition = position;
                return;
            }

            float moved = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(_lastPosition.x, _lastPosition.z));
            if (moved < stepSpacing)
                return;

            JungleWater.Ripple(position, radius, stepStrength);
            _lastPosition = position;
        }
    }
}
