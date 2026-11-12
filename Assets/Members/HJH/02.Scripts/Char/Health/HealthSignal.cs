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

    // 체력 값이 바뀌었다는 알림. 체력바 같은 보는 쪽은 이것만 알면 됨 (누가 어떻게 체력을 들고 있는지 몰라도 됨)
    public interface IHealthSignal
    {
        event Action<HealthChange> Changed;
        HealthChange Last { get; }
    }

    // 플레이어·적 체력을 지켜보다가 바뀌면 Changed 를 쏨.
    // KYR/JJH 체력 코드에 이벤트가 없어서 매 프레임 읽어 비교 (값 바뀐 프레임에만 알림)
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

            // 처음 값은 이전 = 현재 (붙자마자 "깎였다" 연출이 나오지 않게)
            Last = new HealthChange(_hasValue ? Last.Current : current, current, max, shield);
            _hasValue = true;
            Changed?.Invoke(Last);
        }
    }
}
