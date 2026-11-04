using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH.Environment
{
    // 이 씬에서만 다른 URP 설정(그림자 거리·단계 등)을 쓰게 해줌.
    // 팀 공용 PC_RPAsset 은 안 건드리고, 복사본(HJH_Map_RPAsset)을 씬 켜질 때 끼우고 꺼질 때 원래대로 돌려놓음
    [DefaultExecutionOrder(-1000)]
    public class MapRenderSettings : MonoBehaviour
    {
        [SerializeField, Tooltip("이 씬에서 쓸 URP 에셋 (비우면 아무것도 안 함)")]
        private RenderPipelineAsset pipeline;

        private RenderPipelineAsset _previous;
        private bool _applied;

        private void OnEnable()
        {
            if (pipeline == null || QualitySettings.renderPipeline == pipeline)
                return;

            _previous = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = pipeline;
            _applied = true;
        }

        private void OnDisable()
        {
            if (!_applied)
                return;

            QualitySettings.renderPipeline = _previous;
            _applied = false;
        }
    }
}
