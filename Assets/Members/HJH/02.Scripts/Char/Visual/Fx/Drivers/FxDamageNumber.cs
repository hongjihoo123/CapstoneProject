using TMPro;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // Damage number: pops in, flies up and falls with gravity, fades out, always faces the camera.
    // Value = damage amount, Radius = size multiplier, Tint = color.
    public class FxDamageNumber : MonoBehaviour, IFxDriver
    {
        [SerializeField] private TextMeshPro text;
        [SerializeField] private float life = 0.75f;
        [SerializeField] private float riseSpeed = 3.2f;
        [SerializeField] private float gravity = 7f;
        [SerializeField] private float popScale = 1.5f;

        private Vector3 _position;
        private Vector3 _velocity;
        private Color _color;
        private float _scale;
        private float _time;
        private bool _playing;

        public bool IsPlaying => _playing;

        public void Play(in FxParams parameters)
        {
            _color = parameters.Tint;
            _scale = parameters.Radius > 0f ? parameters.Radius : 1f;
            _position = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
            _velocity = new Vector3(Random.Range(-0.8f, 0.8f), riseSpeed, 0f);
            _time = 0f;
            _playing = true;
            text.text = Mathf.RoundToInt(parameters.Value).ToString();
            Apply();
        }

        public void Stop() => _playing = false;

        private void Update()
        {
            if (!_playing)
                return;

            _time += Time.deltaTime;
            if (_time >= life)
            {
                _playing = false;
                text.color = Color.clear;
                return;
            }

            _velocity.y -= gravity * Time.deltaTime;
            _position += _velocity * Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            float pop = _time < 0.08f
                ? Mathf.Lerp(0.3f, popScale, _time / 0.08f)
                : Mathf.Lerp(popScale, 1f, Mathf.Clamp01((_time - 0.08f) / 0.1f));
            float alpha = 1f - Mathf.Clamp01((_time - life * 0.6f) / (life * 0.4f));

            transform.position = _position;
            transform.localScale = Vector3.one * pop * _scale;

            Camera view = Camera.main;
            if (view != null)
                transform.rotation = view.transform.rotation;

            Color color = _color;
            color.a *= alpha;
            text.color = color;
        }
    }
}
