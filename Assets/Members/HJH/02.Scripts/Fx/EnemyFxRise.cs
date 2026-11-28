using System;
using UnityEngine;

namespace HJH.Fx
{
    public class EnemyFxRise : MonoBehaviour
    {
        [Serializable]
        public struct Piece
        {
            public Transform target;
            public float delay;
        }

        [SerializeField] private Piece[] pieces;
        [SerializeField, Min(0.01f)] private float riseTime = 0.16f;
        [SerializeField] private float holdTime = 0.9f;
        [SerializeField, Min(0.01f)] private float sinkTime = 0.4f;
        [SerializeField] private float depth = 1f;

        private Vector3[] _positions;
        private Vector3[] _scales;
        private float _time;

        public void Setup(Piece[] list, float rise, float hold, float sink, float hiddenDepth)
        {
            pieces = list;
            riseTime = rise;
            holdTime = hold;
            sinkTime = sink;
            depth = hiddenDepth;
        }

        private void OnEnable()
        {
            Cache();
            _time = 0f;
            Evaluate(0f);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            Evaluate(_time);
        }

        public void Evaluate(float time)
        {
            Cache();
            for (int i = 0; i < pieces.Length; i++)
            {
                Transform target = pieces[i].target;
                if (target == null) continue;

                float t = time - pieces[i].delay;
                float height;
                if (t <= 0f) height = 0f;
                else if (t < riseTime) height = Overshoot(t / riseTime);
                else if (t < riseTime + holdTime) height = 1f;
                else
                {
                    float k = Mathf.Clamp01((t - riseTime - holdTime) / sinkTime);
                    height = 1f - k * k;
                }

                bool visible = height > 0.001f;
                target.localScale = visible
                    ? Vector3.Scale(_scales[i], new Vector3(Mathf.Min(1f, 0.55f + height * 0.45f), height, Mathf.Min(1f, 0.55f + height * 0.45f)))
                    : Vector3.zero;
                target.localPosition = _positions[i] + Vector3.down * (depth * (1f - Mathf.Min(1f, height)));
            }
        }

        private static float Overshoot(float x)
        {
            float k = x - 1f;
            return 1f + 2.7f * k * k * k + 1.7f * k * k;
        }

        private void Cache()
        {
            if (_positions != null && _positions.Length == pieces.Length) return;

            _positions = new Vector3[pieces.Length];
            _scales = new Vector3[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].target == null) continue;
                _positions[i] = pieces[i].target.localPosition;
                _scales[i] = pieces[i].target.localScale;
            }
        }
    }
}
