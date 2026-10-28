using Members.KYR._01.Scripts.Modules;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01_Scripts.UI
{
    [DefaultExecutionOrder(10000)]
    public class HeadHealthBar : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Image trail;
        [SerializeField] private Image shield;
        [SerializeField] private float clearance = 0.22f;
         private bool hideWhenDead = true;

        [Header("색")]
        [SerializeField] private Color highColor = new(0.32f, 0.82f, 0.38f, 1f);
        [SerializeField] private Color lowColor = new(0.86f, 0.22f, 0.18f, 1f);
        [SerializeField] private float trailSpeed = 0.85f;

        private PlayerHealth _health;
        private Canvas _canvas;
        private float _height = -1f;
        private float _trailRatio = 1f;
        private float _lastHp;
        private bool _trailReady;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            BindHealth();
            _height = MeasureHeight();
            Place();
        }

        private void LateUpdate()
        {
            if (_health == null)
                BindHealth();
            if (_health == null || fill == null)
                return;

            if (_height < 0f)
                _height = MeasureHeight();

            Place();

            float max = _health.MaxHealth;
            float hp = Mathf.Max(0f, _health.CurrentHealth);
            if (max <= 0f)
            {
                SetVisible(false);
                return;
            }

            bool show = !hideWhenDead || hp > 0f;
            SetVisible(show);
            if (!show)
                return;

            float hpRatio = Mathf.Clamp01(hp / max);
            float shieldAmount = Mathf.Max(0f, _health.CurrentShield);
            bool damaged = _trailReady && hp < _lastHp - 0.01f;
            _lastHp = hp;

            if (!damaged || hpRatio > _trailRatio)
                _trailRatio = hpRatio;
            else if (trail != null)
                _trailRatio = Mathf.MoveTowards(_trailRatio, hpRatio, trailSpeed * Time.deltaTime);
            _trailReady = true;

            fill.fillAmount = hpRatio;
            fill.color = Color.Lerp(lowColor, highColor, hpRatio);
            if (trail != null)
                trail.fillAmount = _trailRatio;
            if (shield != null)
            {
                shield.fillAmount = Mathf.Clamp01((hp + shieldAmount) / max);
                shield.enabled = shieldAmount > 0.01f;
            }
        }

        private void SetVisible(bool show)
        {
            if (_canvas != null)
                _canvas.enabled = show;
        }

        private void Place()
        {
            Transform body = Body;
            Vector3 anchor = body.position;
            float lift = Mathf.Max(0f, _height);
            Camera cam = Camera.main;
            Quaternion rotation = cam != null ? cam.transform.rotation : transform.rotation;
            transform.SetPositionAndRotation(anchor + Vector3.up * lift, rotation);
        }

        private Transform Body => _health != null ? _health.transform : transform.parent != null ? transform.parent : transform;

        private void BindHealth()
        {
            _health = GetComponentInParent<PlayerHealth>()
                ?? GetComponentInChildren<PlayerHealth>(true);
        }

        private float MeasureHeight()
        {
            Transform body = Body;
            float top = float.NegativeInfinity;
            Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)
                    continue;
                if (!renderer.enabled)
                    continue;

                top = Mathf.Max(top, renderer.bounds.max.y);
            }

            if (top > float.NegativeInfinity)
                return top - body.position.y + clearance;

            CharacterController controller = body.GetComponent<CharacterController>();
            if (controller != null)
                return controller.center.y + controller.height * 0.5f + clearance;

            return 2f;
        }
    }
}
