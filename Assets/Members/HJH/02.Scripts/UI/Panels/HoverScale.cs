using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 마우스를 올리면 살짝 커짐 (시간이 멈춰 있어도 되게 unscaled). 버튼에 붙여서 인스펙터에서 배율 조절
    public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Min(1f)] private float hoverScale = 1.06f;
        [SerializeField, Min(0.1f)] private float speed = 14f;

        private bool _hover;

        public float Scale
        {
            get => hoverScale;
            set => hoverScale = value;
        }

        private void Update()
        {
            float target = _hover ? hoverScale : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, Time.unscaledDeltaTime * speed);
        }

        private void OnDisable()
        {
            _hover = false;
            transform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hover = true;
        public void OnPointerExit(PointerEventData eventData) => _hover = false;
    }
}
