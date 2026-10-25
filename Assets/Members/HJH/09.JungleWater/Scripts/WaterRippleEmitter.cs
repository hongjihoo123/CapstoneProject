using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 캐릭터(발 위치 = 이 Transform)에 붙이면 물속을 걸을 때 물결이 생긴다.
    public class WaterRippleEmitter : MonoBehaviour
    {
        [SerializeField] private float radius = 0.45f;
        [Tooltip("움직일 때 물결 세기")]
        [SerializeField] private float moveStrength = 0.25f;
        [Tooltip("가만히 서 있을 때 가끔 생기는 물결 세기")]
        [SerializeField] private float idleStrength = 0.06f;
        [Tooltip("이 거리만큼 움직일 때마다 물결 하나")]
        [SerializeField] private float spacing = 0.3f;
        [SerializeField] private float idleInterval = 0.7f;
        [Tooltip("발이 수면보다 이만큼 위여도 물속으로 친다")]
        [SerializeField] private float submergeTolerance = 0.05f;

        private Vector3 _lastPosition;
        private float _idleTimer;

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
            if (moved >= spacing)
            {
                JungleWater.Ripple(position, radius, moveStrength);
                _lastPosition = position;
                _idleTimer = 0f;
                return;
            }

            _idleTimer += Time.deltaTime;
            if (_idleTimer >= idleInterval)
            {
                JungleWater.Ripple(position, radius * 0.8f, idleStrength);
                _idleTimer = 0f;
            }
        }
    }
}
