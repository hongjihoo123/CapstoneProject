using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class HoverSelect : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private UnityEvent onEnter = new();

        public UnityEvent OnEnter => onEnter;

        public void OnPointerEnter(PointerEventData eventData) => onEnter.Invoke();
    }
}
