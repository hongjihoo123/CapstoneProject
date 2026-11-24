using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 픽 화면 가운데 캐릭터를 마우스로 잡고 끌면 좌우로 돌아감 (버튼 위에서 시작한 드래그는 무시)
    public class PickModelSpin : MonoBehaviour
    {
        [SerializeField] private float degreesPerPixel = 0.45f;

        [SerializeField, Tooltip("돌릴 모델이 올라가는 자리 (CharacterPickScreen 의 Stage)")] private Transform stage;

        public Transform Stage
        {
            get => stage;
            set => stage = value;
        }

        private bool _dragging;
        private Vector2 _last;
        private static readonly System.Collections.Generic.List<RaycastResult> Hits = new();

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Stage == null)
                return;

            Vector2 pos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
                _dragging = !OverButton(pos) && Mathf.Abs(pos.x / Screen.width - 0.5f) < 0.17f;
            if (mouse.leftButton.wasReleasedThisFrame)
                _dragging = false;

            if (_dragging && mouse.leftButton.isPressed)
            {
                float dx = pos.x - _last.x;
                Transform model = Stage.childCount > 0 && Stage.GetChild(0).childCount > 0 ? Stage.GetChild(0).GetChild(0) : null;
                if (model != null)
                    model.Rotate(0f, -dx * degreesPerPixel, 0f, Space.World);
            }

            _last = pos;
        }

        private static bool OverButton(Vector2 pos)
        {
            if (EventSystem.current == null)
                return false;

            Hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pos }, Hits);
            foreach (RaycastResult hit in Hits)
            {
                if (hit.gameObject.GetComponentInParent<Selectable>() != null)
                    return true;
            }
            return false;
        }
    }
}
