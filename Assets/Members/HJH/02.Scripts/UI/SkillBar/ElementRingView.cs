using Members.JJH._02_Scripts.ElementsSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
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

        public void SetRingRadius(float radius)
        {
            EnsureMaterial();
            if (_material != null)
                _material.SetFloat(RadiusId, radius);
        }

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
