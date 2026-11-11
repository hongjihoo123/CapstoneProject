using UnityEngine;

namespace Assets.Members.HJH.Environment
{
    // 텍스처를 계속 흘려보냄 (폭포 물줄기용). 머티리얼 복사본을 만들지 않으려고 MaterialPropertyBlock 으로 UV 만 바꿈.
    // 에디터(씬 뷰)에서도 움직임 (ExecuteAlways). 방향이 반대로 보이면 speed 부호를 뒤집으세요.
    //   Cube 옆면 UV 는 v 가 위쪽이라, 물이 아래로 흐르려면 speed.y 가 + 입니다.
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
            float t = Time.realtimeSinceStartup;   // 절대 시간 기준이라 에디터·플레이 어디서나 같게 흐름
            var offset = new Vector2(Mathf.Repeat(speed.x * t, 1f), Mathf.Repeat(speed.y * t, 1f));
            _renderer.GetPropertyBlock(_block);
            var st = new Vector4(tiling.x, tiling.y, offset.x, offset.y);
            _block.SetVector(BaseMapSt, st);
            _block.SetVector(MainTexSt, st);
            _renderer.SetPropertyBlock(_block);

#if UNITY_EDITOR
            // 플레이 중이 아니면 씬 뷰가 알아서 안 갱신하니 20fps 로 다시 그리게 함
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
