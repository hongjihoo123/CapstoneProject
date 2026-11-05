using Members.JJH._02_Scripts.Agents.Enemies;
using Members.JJH._02_Scripts.Agents.Modules;
using Members.JJH._02_Scripts.Systems.ModuleSystem;
using Members.KYR._01.Scripts.Modules;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.UI
{
    [DefaultExecutionOrder(10000)]
    public class HeadHealthBar : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Image trail;
        [SerializeField] private Image shield;

        [Header("Position")]
        [SerializeField] private float fixedHeight = 2f;               // 몸 기준 높이 (위로 올릴수록 값 증가)
        [SerializeField] private Vector3 worldOffset = Vector3.zero;   // 월드 기준 추가 오프셋
        [SerializeField] private Vector3 screenOffset = Vector3.zero;  // 카메라 기준 오프셋 (x:좌우, y:상하, z:앞뒤)

        private bool hideWhenDead = true;

        [Header("Color")]
        [SerializeField] private Color highColor = new(0.32f, 0.82f, 0.38f, 1f);
        [SerializeField] private Color lowColor = new(0.86f, 0.22f, 0.18f, 1f);
        [SerializeField] private float trailSpeed = 0.85f;

        private PlayerHealth _playerHealth;
        private AgentHealth _agentHealth;
        private AbstractEnemy _enemy;
        private Transform _body;
        private Canvas _canvas;
        private float _trailRatio = 1f;
        private float _lastHp;
        private float _peakHp;
        private bool _trailReady;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            BindHealth();
            Place();
        }

        private void LateUpdate()
        {
            if (_playerHealth == null && _agentHealth == null)
                BindHealth();
            if ((_playerHealth == null && _agentHealth == null) || fill == null)
                return;

            Place();

            float hp = ReadHp();
            float max = ReadMax(hp);
            if (max <= 0f)
            {
                SetVisible(false);
                return;
            }

            float hpRatio = Mathf.Clamp01(hp / max);
            bool damaged = _trailReady && hp < _lastHp - 0.01f;
            _lastHp = hp;

            if (!damaged || hpRatio > _trailRatio)
                _trailRatio = hpRatio;
            else if (trail != null)
                _trailRatio = Mathf.MoveTowards(_trailRatio, hpRatio, trailSpeed * Time.deltaTime);
            _trailReady = true;

            bool show = hp > 0f || !hideWhenDead;
            if (_agentHealth != null && hp >= max - 0.01f)
                show = false;
            SetVisible(show);
            if (!show)
                return;

            float shieldAmount = _playerHealth != null ? Mathf.Max(0f, _playerHealth.CurrentShield) : 0f;
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

        private float ReadHp()
        {
            if (_playerHealth != null)
                return Mathf.Max(0f, _playerHealth.CurrentHealth);
            if (_agentHealth != null)
                return Mathf.Max(0f, _agentHealth.CurrentHealth);
            return 0f;
        }

        private float ReadMax(float hp)
        {
            if (_playerHealth != null)
                return _playerHealth.MaxHealth;

            if (hp > _peakHp)
                _peakHp = hp;

            float dataMax = _enemy != null && _enemy.EnemyData != null ? _enemy.EnemyData.Health : 0f;
            return dataMax > 0f ? dataMax : _peakHp;
        }

        private void SetVisible(bool show)
        {
            if (_canvas != null)
                _canvas.enabled = show;
        }

        private void Place()
        {
            Transform body = Body;
            Camera cam = Camera.main;
            Quaternion rotation = cam != null ? cam.transform.rotation : transform.rotation;

            Vector3 pos = body.position + Vector3.up * fixedHeight + worldOffset;
            pos += rotation * screenOffset;

            transform.SetPositionAndRotation(pos, rotation);
        }

        private Transform Body => _body != null ? _body : transform;

        private void BindHealth()
        {
            ModuleOwner owner = GetComponentInParent<ModuleOwner>();
            _body = owner != null ? owner.transform : transform.parent != null ? transform.parent : transform;

            _agentHealth = _body.GetComponentInChildren<AgentHealth>(true);
            if (_agentHealth != null)
            {
                _enemy = _agentHealth.GetComponentInParent<AbstractEnemy>();
                _playerHealth = null;
                return;
            }

            _playerHealth = _body.GetComponentInChildren<PlayerHealth>(true);
        }
    }
}