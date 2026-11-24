using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 마우스를 올리면 OnEnter 이벤트 (인스펙터에서 연결). 픽 화면 스킬 아이콘: 올리면 그 스킬 설명으로
    public class HoverSelect : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private UnityEvent onEnter = new();

        public UnityEvent OnEnter => onEnter;

        public void OnPointerEnter(PointerEventData eventData) => onEnter.Invoke();
    }
}
