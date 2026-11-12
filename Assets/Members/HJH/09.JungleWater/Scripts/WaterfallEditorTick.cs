using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 폭포 물줄기 셰이더는 _Time 으로 흐르는데, 에디터 씬 뷰는 가만히 두면 다시 그리지 않아서 얼어 보인다.
    // 에디터에서만 20fps 로 씬 뷰를 갱신해 줌 (플레이 중·빌드에서는 아무것도 안 함).
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class WaterfallEditorTick : MonoBehaviour
    {
#if UNITY_EDITOR
        private float _next;

        private void Update()
        {
            if (Application.isPlaying || Time.realtimeSinceStartup < _next)
                return;
            _next = Time.realtimeSinceStartup + 0.05f;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
}
