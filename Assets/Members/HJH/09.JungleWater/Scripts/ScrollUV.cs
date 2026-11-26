using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ScrollUV : MonoBehaviour
    {
        [SerializeField, Tooltip("초당 UV 이동량 (x, y)")] private Vector2 speed = new Vector2(0f, 1.6f);
        [SerializeField, Tooltip("UV 반복 횟수 (x, y). 면이 크면 키워서 줄무늬 굵기를 맞춤")] private Vector2 tiling = Vector2.one;

        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");
        private Renderer _renderer;
        private MaterialPropertyBlock _block;
#if UNITY_EDITOR
        private float _nextRepaint;
#endif

        private void OnEnable()
        {
            _renderer = GetComponent<Renderer>();
            _block ??= new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            if (_renderer != null)
                _renderer.SetPropertyBlock(null);
        }

        private void Update()
        {
            if (_renderer == null)
                return;
            _block ??= new MaterialPropertyBlock();
            float t = Time.realtimeSinceStartup;
            var offset = new Vector2(Mathf.Repeat(speed.x * t, 1f), Mathf.Repeat(speed.y * t, 1f));
            _renderer.GetPropertyBlock(_block);
            var st = new Vector4(tiling.x, tiling.y, offset.x, offset.y);
            _block.SetVector(BaseMapSt, st);
            _block.SetVector(MainTexSt, st);
            _renderer.SetPropertyBlock(_block);

#if UNITY_EDITOR
            if (!Application.isPlaying && Time.realtimeSinceStartup > _nextRepaint)
            {
                _nextRepaint = Time.realtimeSinceStartup + 0.05f;
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }
    }
}
