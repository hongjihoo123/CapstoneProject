using UnityEngine;

namespace Assets.Members.HJH.Environment
{
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
