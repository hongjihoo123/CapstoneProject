using Assets.Members.HJH._02.Scripts.Char.Health;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.UI.HealthBar
{
    [RequireComponent(typeof(Canvas))]
    public sealed class HealthBarAutoHide : MonoBehaviour
    {
        [SerializeField] private bool hideWhenDead = true;
        [SerializeField, Tooltip("적만: 체력이 가득이면 숨김")] private bool hideEnemyWhenFull = true;
        [SerializeField, Tooltip("깎인 뒤 잔상이 끝날 때까지는 보이게 (초)")] private float lingerAfterDamage = 1f;

        private Canvas _canvas;
        private IHealthSignal _signal;
        private bool _isPlayer;
        private float _lingerUntil;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _signal = GetComponentInParent<IHealthSignal>();
            _isPlayer = HealthReader.For(this).IsPlayer;
            _canvas.enabled = false;
        }

        private void OnEnable()
        {
            if (_signal != null)
                _signal.Changed += OnChanged;
        }

        private void OnDisable()
        {
            if (_signal != null)
                _signal.Changed -= OnChanged;
        }

        private void OnChanged(HealthChange change)
        {
            if (change.IsDamage)
                _lingerUntil = Time.unscaledTime + lingerAfterDamage;
            Refresh(change);
        }

        private void LateUpdate()
        {
            if (_signal != null && _signal.Last.Max > 0f && Time.unscaledTime < _lingerUntil + 0.1f)
                Refresh(_signal.Last);
        }

        private void Refresh(HealthChange change)
        {
            bool dead = change.Current <= 0f;
            bool full = change.Current >= change.Max - 0.01f;
            bool show = !(hideWhenDead && dead)
                        && !(hideEnemyWhenFull && !_isPlayer && full && Time.unscaledTime >= _lingerUntil);
            _canvas.enabled = show;
        }
    }
}
