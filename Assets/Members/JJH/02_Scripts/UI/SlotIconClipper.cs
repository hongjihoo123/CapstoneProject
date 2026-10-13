using UnityEngine;
using UnityEngine.UI;

namespace Members.JJH._02_Scripts.UI
{
    /// <summary>
    /// 부모(스킬 슬롯 배경)에 붙여서 사용.
    /// 지정한 다각형 구역 안에서만 자식 스킬 아이콘 Image가 보이도록 메시를 잘라낸다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SlotIconClipper : MonoBehaviour
    {
        [Header("자식 UI")]
        [SerializeField] private Image skillIcon;

        [Header("보여줄 구역 - 다각형 (부모 Rect 중앙 기준 로컬 좌표, 3개 이상)")]
        [SerializeField] private Vector2[] polygonPoints =
        {
            new Vector2(0f, 60f),
            new Vector2(60f, 0f),
            new Vector2(0f, -60f),
            new Vector2(-60f, 0f),
        };

        [Header("에디터 미리보기")]
        [SerializeField] private bool showGizmo = true;
        [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0f, 0.8f);

        private PolygonClipEffect _effect;

        private void Awake()
        {
            Setup();
        }

        private void Setup()
        {
            if (skillIcon == null)
            {
                Debug.LogWarning($"[{name}] skillIcon이 연결되지 않았습니다.", this);
                return;
            }

            if (_effect == null)
            {
                _effect = skillIcon.GetComponent<PolygonClipEffect>();
                if (_effect == null) _effect = skillIcon.gameObject.AddComponent<PolygonClipEffect>();
            }

            _effect.Setup((RectTransform)transform, polygonPoints);
        }

        /// <summary>구역을 코드에서 바꾸고 싶을 때 호출</summary>
        public void SetPolygon(Vector2[] points)
        { 
            polygonPoints = points;
            if (_effect != null) _effect.Setup((RectTransform)transform, polygonPoints);
        }    
           
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && _effect != null)
                _effect.Setup((RectTransform)transform, polygonPoints);
        }

        private void OnDrawGizmos()
        {
            if (!showGizmo || polygonPoints == null || polygonPoints.Length < 2) return;
            var rt = transform as RectTransform;
            if (rt == null) return;

            Gizmos.color = gizmoColor;
            Vector2 center = rt.rect.center;

            for (int i = 0; i < polygonPoints.Length; i++)
            {
                Vector3 a = rt.TransformPoint(center + polygonPoints[i]);
                Vector3 b = rt.TransformPoint(center + polygonPoints[(i + 1) % polygonPoints.Length]);
                Gizmos.DrawLine(a, b);
            } 
        } 
#endif
    }
}