using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Drives the SG_FX_MagicCircle material on the circle quad: _Fill (Value 0..1), _RotateSpeed (Speed, deg/s),
    // and a pop-in / fade-out through the _Tint alpha.
    public class FxMagicCircleDriver : MonoBehaviour, IFxDriver, IFxValueReceiver, IFxSpeedReceiver, IFxTintReceiver
    {
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int RotateSpeedId = Shader.PropertyToID("_RotateSpeed");
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        [SerializeField] private MeshRenderer circle;
        [SerializeField] private float appearTime = 0.2f;
        [SerializeField] private float fadeTime = 0.25f;
        [SerializeField, Tooltip("Start scale while appearing.")] private float appearScale = 0.6f;

        private MaterialPropertyBlock _block;
        private Color _tint = Color.white;
        private float _time;
        private float _fadeStart = -1f;
        private bool _playing;

        public bool IsPlaying => _playing;

        public void Play(in FxParams parameters)
        {
            _block ??= new MaterialPropertyBlock();
            _tint = parameters.Tint;
            _time = 0f;
            _fadeStart = -1f;
            _playing = true;
            SetValue(0f);
            SetSpeed(90f);
            Apply();
        }

        public void Stop()
        {
            if (_fadeStart < 0f)
                _fadeStart = _time;
        }

        public void SetValue(float value) => SetFloat(FillId, Mathf.Clamp01(value));

        public void SetSpeed(float degreesPerSecond) => SetFloat(RotateSpeedId, degreesPerSecond * Mathf.Deg2Rad);

        public void SetTint(Color tint) => _tint = tint;

        private void Update()
        {
            if (!_playing)
                return;

            _time += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            float appear = Mathf.Clamp01(_time / appearTime);
            float alpha = appear;
            if (_fadeStart >= 0f)
            {
                alpha *= 1f - Mathf.Clamp01((_time - _fadeStart) / fadeTime);
                if (alpha <= 0f)
                    _playing = false;
            }

            circle.transform.localScale = Vector3.one * Mathf.Lerp(appearScale, 1f, 1f - (1f - appear) * (1f - appear));

            Color color = _tint;
            color.a *= alpha;
            circle.GetPropertyBlock(_block);
            _block.SetColor(TintId, color);
            circle.SetPropertyBlock(_block);
        }

        private void SetFloat(int id, float value)
        {
            _block ??= new MaterialPropertyBlock();
            circle.GetPropertyBlock(_block);
            _block.SetFloat(id, value);
            circle.SetPropertyBlock(_block);
        }
    }
}
