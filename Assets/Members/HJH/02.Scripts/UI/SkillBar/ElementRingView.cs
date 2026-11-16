using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 스킬 칸 테두리 바깥에 속성 모양 링(셰이더 HJH/UI/ElementRing)을 씌우는 부품.
    //  - 칸보다 크게(약 1.9배) 깔고 스프라이트는 비워 둔다. 클릭은 안 받는다.
    //  - SetIntensity 로 세기를 바꾸면 부드럽게 따라감: 0.5 약함 / 1 기본 / 2.4 "이걸 쓰세요"
    [RequireComponent(typeof(Image))]
    public class ElementRingView : MonoBehaviour
    {
        public const string ShaderName = "HJH/UI/ElementRing";

        private static readonly int ElementId = Shader.PropertyToID("_Element");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        [SerializeField, Min(0.01f), Tooltip("Higher = intensity follows the target faster.")]
        private float followSpeed = 14f;

        private Image _image;
        private Material _material;
        private float _target = 1f;
        private float _current = 1f;

        private void Awake() => EnsureMaterial();

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        private void Update()
        {
            if (_material == null || Mathf.Approximately(_current, _target))
                return;

            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
            if (Mathf.Abs(_current - _target) < 0.002f)
                _current = _target;
            _material.SetFloat(IntensityId, _current);
        }

        public void Setup(ElementType element, float seed = 0f)
        {
            EnsureMaterial();
            _material.SetFloat(ElementId, ToShaderIndex(element));
            _material.SetFloat(SeedId, seed);
            _material.SetFloat(IntensityId, _current);
        }

        private static readonly int RadiusId = Shader.PropertyToID("_RingRadius");

        // 링 반지름 (정사각형 절반 = 1 기준). 칸 가장자리에 링이 오도록 칸 크기/링 크기 비율로 넣어줌
        public void SetRingRadius(float radius)
        {
            EnsureMaterial();
            if (_material != null)
                _material.SetFloat(RadiusId, radius);
        }

        // snap = true 면 바로 그 값으로 (처음 세팅 때)
        public void SetIntensity(float value, bool snap = false)
        {
            _target = Mathf.Max(0f, value);
            if (!snap)
                return;

            _current = _target;
            if (_material != null)
                _material.SetFloat(IntensityId, _current);
        }

        private void EnsureMaterial()
        {
            if (_material != null)
                return;

            _image = GetComponent<Image>();
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[ElementRingView] Shader not found: {ShaderName}", this);
                return;
            }

            _material = new Material(shader) { name = "ElementRing (instance)" };
            _image.material = _material;
            _image.raycastTarget = false;
        }

        // 셰이더의 _Element 번호 (Fire 0, Water 1, Wind 2, Electric 3, Earth 4) - ElementType 순서와 같음
        private static float ToShaderIndex(ElementType element) => element switch
        {
            ElementType.Fire => 0f,
            ElementType.Water => 1f,
            ElementType.Wind => 2f,
            ElementType.Electric => 3f,
            _ => 4f
        };
    }
}
