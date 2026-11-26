using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH.Environment
{
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
