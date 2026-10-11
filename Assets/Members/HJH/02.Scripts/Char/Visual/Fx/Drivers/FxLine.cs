using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Straight line effect (beam, tracer, streak) between two runtime points.
    // Width over the effect's life comes from the curve; color = Tint x alpha curve.
    // Optional points/particles: startPoint/endPoint are moved to the ends (put flashes under them),
    // alongLine systems get a box shape stretched over the whole line.
    // Duration <= 0 holds the line until Kill (aim guides), and SetPoints can re-aim it every frame.
    public class FxLine : MonoBehaviour, IFxDriver, IFxPointsReceiver, IFxTintReceiver, IFxValueReceiver
    {
        [SerializeField] private LineRenderer[] lines;
        [SerializeField, Tooltip("Width of each line relative to the requested width (same order as Lines).")]
        private float[] widthScale = { 1f };
        [SerializeField, Tooltip("Width over normalized life (0..1).")]
        private AnimationCurve widthOverLife = new(new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1.25f), new Keyframe(1f, 0f));
        [SerializeField] private AnimationCurve alphaOverLife = new(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        [SerializeField, Tooltip("Lines that stay white (cores).")] private LineRenderer[] untinted;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;
        [SerializeField] private ParticleSystem[] alongLine;

        private float _width;
        private float _duration;
        private float _time;
        private bool _playing;
        private Color _tint = Color.white;
        private float _valueScale = 1f;

        public bool IsPlaying => _playing;

        public void Play(in FxParams parameters)
        {
            _width = parameters.Width > 0f ? parameters.Width : 1f;
            _duration = parameters.Duration;
            _time = 0f;
            _playing = true;
            _tint = parameters.Tint;
            _valueScale = parameters.Value > 0f ? parameters.Value : 1f;
            SetPoints(parameters.Start, parameters.End);
            Apply(0f);
        }

        public void Stop()
        {
            // Held lines fade out quickly once killed.
            if (_duration <= 0f)
            {
                _duration = 0.1f;
                _time = 0f;
            }
        }

        public void SetTint(Color tint) => _tint = tint;

        // Thickness multiplier while the line is held (e.g. a barrel thickening as it charges).
        public void SetValue(float value) => _valueScale = Mathf.Max(0f, value);

        public void SetPoints(Vector3 start, Vector3 end)
        {
            foreach (LineRenderer line in lines)
            {
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPosition(0, start);
                line.SetPosition(1, end);
            }

            if (startPoint != null)
                startPoint.position = start;
            if (endPoint != null)
                endPoint.position = end;

            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (alongLine == null || length < 0.01f)
                return;

            foreach (ParticleSystem system in alongLine)
            {
                system.transform.SetPositionAndRotation(start + delta * 0.5f, Quaternion.LookRotation(delta));
                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(0.2f, 0.2f, length / Mathf.Max(0.001f, system.transform.lossyScale.z));
            }
        }

        private void Update()
        {
            if (!_playing)
                return;

            if (_duration <= 0f)
            {
                Apply(0.2f);
                return;
            }

            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / _duration);
            Apply(t);
            if (t >= 1f)
                _playing = false;
        }

        private void Apply(float t)
        {
            float width = _width * _valueScale * widthOverLife.Evaluate(t);
            float alpha = alphaOverLife.Evaluate(t);

            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer line = lines[i];
                float scale = i < widthScale.Length ? widthScale[i] : 1f;
                line.widthMultiplier = width * scale;

                bool keepWhite = untinted != null && System.Array.IndexOf(untinted, line) >= 0;
                Color color = keepWhite ? Color.white : _tint;
                color.a *= alpha;
                line.startColor = line.endColor = color;
            }
        }
    }
}
