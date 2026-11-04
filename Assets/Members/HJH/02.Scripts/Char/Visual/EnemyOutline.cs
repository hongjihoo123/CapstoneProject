using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 적 테두리: 평소 빨강(머티리얼 값), 마우스를 올리면 흰색.
    //  - 만화풍 적 변형 프리팹(HJH/Enemy/만화풍 적 만들기)에 붙어 있음. 셰이더 HJH/Character/Toon 의 단색 외곽선(_OutlineSolid) 을 씀
    //  - 마우스 광선은 프레임마다 한 번만 쏨 (적이 많아도 같음), 색은 바뀔 때만 MaterialPropertyBlock 으로
    [DisallowMultipleComponent]
    public class EnemyOutline : MonoBehaviour
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static int _rayFrame = -1;
        private static EnemyOutline _hovered;

        [SerializeField] private Color normal = new(0.95f, 0.12f, 0.1f, 1f);
        [SerializeField] private Color hover = Color.white;
        [SerializeField, Tooltip("마우스 광선 굵기 (m). 작은 적도 잘 잡히게")] private float pickRadius = 0.35f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private bool _shownHover;
        private bool _initialized;

        public static EnemyOutline Hovered => _hovered;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            if (_hovered == this)
                _hovered = null;
            Apply(false);
        }

        private void Update()
        {
            if (_rayFrame != Time.frameCount)
            {
                _rayFrame = Time.frameCount;
                _hovered = Pick();
            }
            bool hovered = _hovered == this;
            if (hovered != _shownHover || !_initialized)
                Apply(hovered);
        }

        private EnemyOutline Pick()
        {
            Camera cam = Camera.main;
            Mouse mouse = Mouse.current;
            if (cam == null || mouse == null)
                return null;
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.SphereCast(ray, pickRadius, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Collide))
                return hit.collider.GetComponentInParent<EnemyOutline>();
            return null;
        }

        private void Apply(bool hovered)
        {
            _initialized = true;
            _shownHover = hovered;
            if (_renderers == null)
                return;
            foreach (Renderer r in _renderers)
            {
                if (r == null || r is ParticleSystemRenderer)
                    continue;
                Material m = r.sharedMaterial;
                if (m == null || !m.HasProperty(OutlineColorId))
                    continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(OutlineColorId, hovered ? hover : normal);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
