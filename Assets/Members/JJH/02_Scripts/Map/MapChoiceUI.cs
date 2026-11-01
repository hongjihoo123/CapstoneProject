
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Members.JJH._02_Scripts.Map
{
    public class MapChoiceUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI")]
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI roomName;

        [Header("Outline")]
        [SerializeField] private Outline outline;
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField] private Vector2 outlineEffectDistance = new(4f, 4f);

        [Header("Event")]
        [SerializeField] private EventChannelSO eventChannel;

        private MapUI.RoomOption _roomOption;

        public void Initialize(MapUI.RoomOption roomOption)
        {
            _roomOption = roomOption;

            roomName.text = roomOption.displayName;
            background.sprite = roomOption.backgroundSprite;

            if (outline == null)
                outline = background.GetComponent<Outline>();

            if (outline == null)
                outline = background.gameObject.AddComponent<Outline>();

            outline.effectColor = outlineColor;
            outline.effectDistance = outlineEffectDistance;
            outline.enabled = false;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button.interactable && outline != null)
                outline.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (outline != null)
                outline.enabled = false;
        }

        private void OnClick()
        {
            eventChannel.RaiseEvent(SystemEvents.SelectRoomEvent.Init(this, _roomOption));
        }
    }
}
