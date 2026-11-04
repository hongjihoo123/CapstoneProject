using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH._02.Scripts.Map
{
    // 화면 스타일라이즈 설정 (Volume 의 Add Override → HJH/Stylize).
    // 렌더러의 "HJH Stylize" 전체 화면 패스(StylizeFullscreen.shader)가 이 값을 씀.
    // Intensity 가 0 인 씬(= 이 항목이 없는 씬)은 화면이 그대로.
    [Serializable, VolumeComponentMenu("HJH/Stylize")]
    public sealed class StylizeVolume : VolumeComponent
    {
        [Tooltip("원본과 섞는 정도. 0 = 꺼짐")]
        public ClampedFloatParameter intensity = new(0f, 0f, 1f);
        [Tooltip("붓칠(쿠와하라) 반경 픽셀. 클수록 잔무늬가 넓은 색 면으로 뭉개짐. 0 = 끔")]
        public ClampedIntParameter paintRadius = new(3, 0, 4);
        [Tooltip("명암 단계 수")]
        public ClampedFloatParameter bands = new(3f, 2f, 8f);
        [Tooltip("명암 단계를 얼마나 세게 나눌지")]
        public ClampedFloatParameter bandStrength = new(0.45f, 0f, 1f);
        [Tooltip("외곽선 진하기")]
        public ClampedFloatParameter outline = new(0.7f, 0f, 1f);
        [Tooltip("이 거리(m)보다 먼 곳은 외곽선이 사라짐")]
        public MinFloatParameter outlineDistance = new(45f, 1f);
    }

    // 매 프레임 Volume 값을 셰이더 전역값으로 넘김 (씬에 따로 놓을 필요 없음)
    internal static class StylizeDriver
    {
        private static readonly int On = Shader.PropertyToID("_HJH_StylizeOn");
        private static readonly int Intensity = Shader.PropertyToID("_HJH_StylizeIntensity");
        private static readonly int Radius = Shader.PropertyToID("_HJH_PaintRadius");
        private static readonly int Bands = Shader.PropertyToID("_HJH_Bands");
        private static readonly int BandStrength = Shader.PropertyToID("_HJH_BandStrength");
        private static readonly int Outline = Shader.PropertyToID("_HJH_Outline");
        private static readonly int OutlineDistance = Shader.PropertyToID("_HJH_OutlineDistance");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            RenderPipelineManager.beginContextRendering -= Push;
            RenderPipelineManager.beginContextRendering += Push;
        }

#if UNITY_EDITOR
        // 플레이 안 할 때도 씬 뷰·게임 뷰에서 보이게
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitEditor() => Init();
#endif

        private static void Push(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
        {
            VolumeStack stack = VolumeManager.instance.stack;
            StylizeVolume s = stack?.GetComponent<StylizeVolume>();
            bool on = s != null && s.intensity.value > 0f;
            Shader.SetGlobalFloat(On, on ? 1f : 0f);
            if (!on)
                return;
            Shader.SetGlobalFloat(Intensity, s.intensity.value);
            Shader.SetGlobalFloat(Radius, s.paintRadius.value);
            Shader.SetGlobalFloat(Bands, s.bands.value);
            Shader.SetGlobalFloat(BandStrength, s.bandStrength.value);
            Shader.SetGlobalFloat(Outline, s.outline.value);
            Shader.SetGlobalFloat(OutlineDistance, s.outlineDistance.value);
        }
    }
}
