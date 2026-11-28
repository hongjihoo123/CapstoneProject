using UnityEngine;

namespace HJH.Fx
{
    public class EnemyFxSpin : MonoBehaviour
    {
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(360f, 0f, 0f);
        [SerializeField] private float hopHeight;
        [SerializeField] private float hopsPerSecond = 3f;
        [SerializeField] private bool randomStart = true;

        private Vector3 _basePosition;
        private float _offset;

        public void Setup(Vector3 spin, float hop, float rate)
        {
            degreesPerSecond = spin;
            hopHeight = hop;
            hopsPerSecond = rate;
        }

        public void Preview(float time)
        {
            transform.Rotate(degreesPerSecond * time, Space.Self);
        }

        private void Awake()
        {
            _basePosition = transform.localPosition;
        }

        private void OnEnable()
        {
            _offset = randomStart ? Random.value : 0f;
            if (randomStart) transform.Rotate(degreesPerSecond.normalized * (_offset * 360f), Space.Self);
        }

        private void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
            if (hopHeight <= 0f) return;

            float bounce = Mathf.Abs(Mathf.Sin((Time.time * hopsPerSecond + _offset) * Mathf.PI));
            transform.localPosition = _basePosition + Vector3.up * (bounce * hopHeight);
        }
    }
}
