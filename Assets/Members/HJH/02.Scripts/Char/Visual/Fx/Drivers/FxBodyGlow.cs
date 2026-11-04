using Members.KYR._01_Scripts;
using Members.KYR._01.Scripts.Player;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 오라 이펙트에 붙이면, 켜져 있는 동안 플레이어 몸이 빛남 (각성·변신 느낌).
    // 만화풍 캐릭터 셰이더(HJH/Character/Toon)의 테두리 빛(_RimColor/_RimStrength/_RimPower)을 MaterialPropertyBlock 으로 올리고
    // 맥박처럼 일렁이게 함. 꺼지면 천천히 원래 값으로 돌아감. 다른 셰이더면 아무 일도 안 함.
    public class FxBodyGlow : MonoBehaviour, IFxDriver, IFxTintReceiver
    {
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

        [SerializeField, ColorUsage(false, true)] private Color glow = new(1.6f, 0.65f, 0.15f);
        [SerializeField, Tooltip("테두리 빛 세기 (평소 0.45)")] private float rimStrength = 1.8f;
        [SerializeField, Tooltip("작을수록 테두리 빛이 넓게 퍼짐 (평소 3.5)")] private float rimPower = 1.6f;
        [SerializeField, Tooltip("몸 전체 밝기 (평소 1.05)")] private float brightness = 1.15f;
        [SerializeField, Tooltip("일렁이는 빠르기")] private float pulseSpeed = 7f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.25f;
        [SerializeField] private float fadeIn = 0.12f;
        [SerializeField] private float fadeOut = 0.45f;
        [SerializeField, Tooltip("켜는 순간 잠깐 더 세게 (번쩍)")] private float flash = 1.8f;

        private Renderer[] _targets;
        private MaterialPropertyBlock _block;
        private float _weight;
        private float _flash;
        private bool _on;

        public bool IsPlaying => _on || _weight > 0.001f;

        public void Play(in FxParams parameters)
        {
            _targets = FindBody();
            _block ??= new MaterialPropertyBlock();
            _on = true;
            _flash = 1f;
        }

        public void Stop() => _on = false;

        public void SetTint(Color tint)
        {
            // 원소 색으로 물들이면 그 색으로 빛남 (흰색이면 기본 불꽃색)
            if (tint != Color.white)
                glow = new Color(tint.r * 1.6f, tint.g * 1.6f, tint.b * 1.6f);
        }

        private static Renderer[] FindBody()
        {
            PlayerAgent player = FindAnyObjectByType<PlayerAgent>();
            return player != null ? player.GetComponentsInChildren<SkinnedMeshRenderer>() : new Renderer[0];
        }

        private void LateUpdate()
        {
            if (_targets == null || _targets.Length == 0)
                return;

            float dt = Time.deltaTime;
            _weight = Mathf.MoveTowards(_weight, _on ? 1f : 0f, dt / Mathf.Max(0.01f, _on ? fadeIn : fadeOut));
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 3f);
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount + _flash * flash;

            foreach (Renderer r in _targets)
            {
                if (r == null)
                    continue;
                Material m = r.sharedMaterial;
                if (m == null || !m.HasProperty(RimColorId))
                    continue;

                r.GetPropertyBlock(_block);
                _block.SetColor(RimColorId, Color.Lerp(m.GetColor(RimColorId), glow, _weight));
                _block.SetFloat(RimStrengthId, Mathf.Lerp(m.GetFloat(RimStrengthId), rimStrength * pulse, _weight));
                _block.SetFloat(RimPowerId, Mathf.Lerp(m.GetFloat(RimPowerId), rimPower, _weight));
                _block.SetFloat(BrightnessId, Mathf.Lerp(m.GetFloat(BrightnessId), brightness, _weight));
                r.SetPropertyBlock(_block);
            }

            if (_weight <= 0.001f && !_on)
                _targets = null;   // 다 돌아왔으면 손 뗌 (원래 값으로 덮어 둔 상태)
        }
    }
}
