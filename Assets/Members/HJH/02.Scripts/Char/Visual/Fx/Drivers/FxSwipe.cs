using RobotWeapons;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Weapon slash: a ribbon whose head sweeps around the follow target with a fading tail.
    // The slash plane / arc / width come from SwingShape (set per combo step on the weapon data).
    // tip = an optional child (e.g. glow particles) that rides the head while it sweeps.
    public class FxSwipe : MonoBehaviour, IFxDriver, IFxTintReceiver
    {
        private static readonly Quaternion Flat = Quaternion.LookRotation(Vector3.up, Vector3.forward);

        [SerializeField] private LineRenderer line;
        [SerializeField] private Transform tip;
        [SerializeField, Min(2)] private int points = 20;
        [SerializeField, Tooltip("Visible tail length as a share of the arc.")] private float tailLength = 0.65f;
        [SerializeField] private float fadeTime = 0.1f;
        [SerializeField, Tooltip("Ribbon width relative to SwingShape.width (thin edge accents < 1).")]
        private float widthScale = 1f;

        private Transform _origin;
        private SwingShape _shape;
        private float _radius;
        private float _delay;
        private float _duration;
        private float _time;
        private bool _playing;
        private Color _tint = Color.white;

        public bool IsPlaying => _playing;

        public void Play(in FxParams parameters)
        {
            _origin = parameters.Follow;
            _shape = parameters.Shape ?? new SwingShape();
            _radius = parameters.Radius;
            _delay = parameters.Delay;
            _duration = Mathf.Max(0.02f, parameters.Duration);
            _tint = parameters.Tint;
            _time = 0f;
            _playing = _origin != null;

            line.useWorldSpace = true;
            line.positionCount = 0;
            line.alignment = _shape.faceCamera ? LineAlignment.View : LineAlignment.TransformZ;
            SetTipActive(false);
        }

        public void Stop() => _playing = false;

        public void SetTint(Color tint) => _tint = tint;

        private void Update()
        {
            if (!_playing)
                return;

            _time += Time.deltaTime;
            if (_origin == null)
            {
                Finish();
                return;
            }

            float local = _time - _delay;
            if (local < 0f)
                return;

            float sweep = Mathf.Clamp01(local / _duration);
            float fade = Mathf.Clamp01((local - _duration) / fadeTime);
            if (fade >= 1f)
            {
                Finish();
                return;
            }

            float arc = _shape.arc;
            float start = -arc * 0.5f;
            float head = Mathf.Lerp(start, arc * 0.5f, 1f - (1f - sweep) * (1f - sweep));
            float tail = Mathf.Max(start, head - arc * tailLength) + (head - start) * fade;
            float sign = _shape.clockwise ? 1f : -1f;

            Vector3 forward = _origin.forward;
            forward.y = 0f;
            Quaternion facing = Quaternion.LookRotation(forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward) * _shape.PlaneRotation;
            Vector3 center = _origin.position + Vector3.up * _shape.height;

            // Ribbon lies in the slash plane (ignored when the line faces the camera).
            line.transform.rotation = Quaternion.Normalize(facing * Flat);
            line.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float a = Mathf.Lerp(tail, head, i / (points - 1f)) * sign;
                line.SetPosition(i, center + facing * (Quaternion.Euler(0f, a, 0f) * Vector3.forward) * _radius);
            }

            line.widthMultiplier = _shape.width * widthScale * (1f - fade);
            line.colorGradient = Gradient(_tint, 1f - fade);

            bool sweeping = sweep < 1f;
            SetTipActive(sweeping);
            if (sweeping && tip != null)
                tip.position = center + facing * (Quaternion.Euler(0f, head * sign, 0f) * Vector3.forward) * _radius;
        }

        private void Finish()
        {
            _playing = false;
            line.positionCount = 0;
            SetTipActive(false);
        }

        private void SetTipActive(bool active)
        {
            if (tip == null)
                return;

            foreach (ParticleSystem system in tip.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = active;
            }
        }

        private static Gradient Gradient(Color color, float headAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(headAlpha, 1f) });
            return gradient;
        }
    }
}
