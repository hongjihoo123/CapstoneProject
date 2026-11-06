using TMPro;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 데미지 숫자 (Shape of Dreams 식, StudyNotes/SoD_타격감_분석.md):
    //   크게 팝 → 잠깐(~0.1초) 그 크기로 멈춤 → 약 60% 로 줄면서 위로 살짝 → 작게 떠 있다가 → 사라짐
    // 크기 변화 자체가 "쾅" 하는 느낌을 만듦. 크리는 더 크게 튀고 흰 외곽선.
    // Value = 숫자, Radius = 크기 배율, Tint = 색, NumberStyle = 일반/크리/받은 피해. 항상 카메라를 봄
    public class FxDamageNumber : MonoBehaviour, IFxDriver
    {
        [SerializeField] private TextMeshPro text;

        [Header("움직임 (초)")]
        [SerializeField] private float popInTime = 0.04f;
        [SerializeField, Tooltip("가장 클 때 멈춰 있는 시간")] private float popHoldTime = 0.1f;
        [SerializeField] private float shrinkTime = 0.08f;
        [SerializeField, Tooltip("줄어든 뒤 떠 있는 시간")] private float floatTime = 0.5f;
        [SerializeField] private float fadeOutTime = 0.2f;

        [Header("크기 (줄어든 뒤 = 1)")]
        [SerializeField, Tooltip("가장 클 때 크기: x 일반 / y 크리 / z 받은 피해")]
        private Vector3 peakScale = new(1.4f, 1.7f, 1.3f);

        [Header("위로 뜨기")]
        [SerializeField, Tooltip("줄어들면서 위로 올라가는 거리")] private float shrinkRise = 0.35f;
        [SerializeField, Tooltip("떠 있는 동안 천천히 올라가는 속도")] private float floatSpeed = 0.3f;

        [Header("외곽선")]
        [SerializeField] private Color critOutline = Color.white;
        [SerializeField, Range(0f, 1f)] private float critOutlineWidth = 0.3f;
        [SerializeField] private Color normalOutline = new(0.12f, 0.04f, 0.02f, 1f);
        [SerializeField, Range(0f, 1f)] private float normalOutlineWidth = 0.18f;

        private Vector3 _position;
        private Color _color;
        private float _scale;
        private float _peak;
        private float _time;
        private bool _playing;
        private TMP_FontAsset _defaultFont;

        private static TMP_FontAsset _koreanFont;
        private static bool _koreanLoaded;

        // TextMesh Pro 기본 Resources 폴더의 한글 글꼴
        private static TMP_FontAsset KoreanFont
        {
            get
            {
                if (!_koreanLoaded)
                {
                    _koreanLoaded = true;
                    _koreanFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Pretendard-Medium SDF");
                }
                return _koreanFont;
            }
        }

        public bool IsPlaying => _playing;

        private float ShrinkEnd => popInTime + popHoldTime + shrinkTime;
        private float Life => ShrinkEnd + floatTime + fadeOutTime;

        public void Play(in FxParams parameters)
        {
            _color = parameters.Tint;
            _scale = parameters.Radius > 0f ? parameters.Radius : 1f;
            _position = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
            _time = 0f;
            _playing = true;

            DamageNumberStyle style = parameters.NumberStyle;
            _peak = style switch
            {
                DamageNumberStyle.Crit => peakScale.y,
                DamageNumberStyle.Taken => peakScale.z,
                _ => peakScale.x
            };

            // 글자(한글)는 숫자 글꼴에 없어서 한글 글꼴로 바꿔 씀. 숫자로 돌아오면 원래 글꼴
            _defaultFont ??= text.font;
            bool isText = !string.IsNullOrEmpty(parameters.Text);
            TMP_FontAsset korean = isText ? KoreanFont : null;
            text.font = isText && korean != null ? korean : _defaultFont;
            text.text = isText ? parameters.Text : Mathf.RoundToInt(parameters.Value).ToString();

            bool crit = style == DamageNumberStyle.Crit;
            text.outlineColor = crit ? critOutline : normalOutline;
            text.outlineWidth = crit ? critOutlineWidth : normalOutlineWidth;

            Apply();
        }

        public void Stop() => _playing = false;

        private void Update()
        {
            if (!_playing)
                return;

            _time += Time.deltaTime;
            if (_time >= Life)
            {
                _playing = false;
                text.color = Color.clear;
                return;
            }

            Apply();
        }

        private void Apply()
        {
            float holdEnd = popInTime + popHoldTime;
            float shrinkEnd = ShrinkEnd;

            float size;
            float rise;
            if (_time < popInTime)
            {
                float t = _time / Mathf.Max(0.001f, popInTime);
                size = Mathf.Lerp(0.4f, _peak, 1f - (1f - t) * (1f - t));   // 빨리 커졌다가 끝에서 멈춤
                rise = 0f;
            }
            else if (_time < holdEnd)
            {
                size = _peak;
                rise = 0f;
            }
            else if (_time < shrinkEnd)
            {
                float t = Mathf.SmoothStep(0f, 1f, (_time - holdEnd) / Mathf.Max(0.001f, shrinkTime));
                size = Mathf.Lerp(_peak, 1f, t);
                rise = shrinkRise * t;
            }
            else
            {
                size = 1f;
                rise = shrinkRise + floatSpeed * (_time - shrinkEnd);
            }

            float alpha = 1f - Mathf.Clamp01((_time - (Life - fadeOutTime)) / Mathf.Max(0.001f, fadeOutTime));

            transform.position = _position + Vector3.up * rise;
            transform.localScale = Vector3.one * size * _scale;

            Camera view = Camera.main;
            if (view != null)
                transform.rotation = view.transform.rotation;

            Color color = _color;
            color.a *= alpha;
            text.color = color;
        }
    }
}
