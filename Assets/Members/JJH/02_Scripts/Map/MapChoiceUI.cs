
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Members.JJH._02_Scripts.Map.MapUI;

namespace Members.JJH._02_Scripts.Map
{
    public class MapChoiceUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI")]
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI roomName;          // 맵 이름
        [SerializeField] private TextMeshProUGUI categoryText;      // 분류 (일반 전투 / 상점 ...)
        [SerializeField] private TextMeshProUGUI difficultyText;    // 오른쪽 위 태그 (NORMAL ...)

        [Header("Type Style (RoomType 순서: Normal, Elite, Upgrade, Shop, Boss)")]
        [SerializeField] private string[] typeTags;
        [SerializeField] private Color[] typeColors;

        [Header("Outline")]
        [SerializeField] private Outline outline;
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField] private Vector2 outlineEffectDistance = new(4f, 4f);

        [Header("Event")]
        [SerializeField] private EventChannelSO mapChannel;

        private RoomOption _roomOption;

        public void Initialize(RoomOption roomOption)
        {
            _roomOption = roomOption;

            roomName.text = string.IsNullOrWhiteSpace(roomOption.mapName) ? roomOption.displayName : roomOption.mapName;
            background.sprite = roomOption.backgroundSprite;

            int typeIndex = (int)roomOption.type;
            if (categoryText != null)
                categoryText.text = roomOption.displayName;
            if (difficultyText != null)
            {
                if (typeTags != null && typeIndex < typeTags.Length)
                    difficultyText.text = typeTags[typeIndex];
                if (typeColors != null && typeIndex < typeColors.Length)
                    difficultyText.color = typeColors[typeIndex];
            }

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
            mapChannel.RaiseEvent(MapEvents.SelectRoomEvent.Init(this, _roomOption));
        }
    }
}
