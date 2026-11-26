using System;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Health
{
    public readonly struct HealthChange
    {
        public readonly float Previous;
        public readonly float Current;
        public readonly float Max;
        public readonly float Shield;

        public HealthChange(float previous, float current, float max, float shield)
        {
            Previous = previous;
            Current = current;
            Max = max;
            Shield = shield;
        }

        public float PreviousRatio => Max > 0f ? Mathf.Clamp01(Previous / Max) : 0f;
        public float Ratio => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;
        public bool IsDamage => Current < Previous - 0.001f;
        public bool IsHeal => Current > Previous + 0.001f;
    }

    public interface IHealthSignal
    {
        event Action<HealthChange> Changed;
        HealthChange Last { get; }
    }

    [DefaultExecutionOrder(9000)]
    public sealed class HealthSignal : MonoBehaviour, IHealthSignal
    {
        public event Action<HealthChange> Changed;
        public HealthChange Last { get; private set; }

        private HealthReader _reader;
        private bool _hasValue;

        private void OnEnable() => _hasValue = false;

        private void LateUpdate()
        {
            if (!_reader.IsValid)
            {
                _reader = HealthReader.For(this);
                if (!_reader.IsValid)
                    return;
            }

            float current = _reader.Current;
            float max = _reader.Max;
            float shield = _reader.Shield;
            if (max <= 0f)
                return;

            if (_hasValue
                && Mathf.Abs(current - Last.Current) < 0.001f
                && Mathf.Abs(max - Last.Max) < 0.001f
                && Mathf.Abs(shield - Last.Shield) < 0.001f)
                return;

            Last = new HealthChange(_hasValue ? Last.Current : current, current, max, shield);
            _hasValue = true;
            Changed?.Invoke(Last);
        }
    }
}
