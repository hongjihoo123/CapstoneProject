using UnityEngine;
using UnityEngine.UI;

namespace Members.JJH._02_Scripts.Map
{
    public class MapRoomUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button button;
        [SerializeField] private Image roomIcon;
        [SerializeField] private Image clearIcon;
        [SerializeField] private Image background;
        [Header("Icons")]
        [SerializeField] private Sprite defaultIcon;
        [SerializeField] private Sprite hideIcon;
        [SerializeField] private Sprite normalIcon;
        [SerializeField] private Sprite eliteIcon;
        [SerializeField] private Sprite bossIcon;
        [SerializeField] private Sprite shopIcon;
        [SerializeField] private Sprite clearSprite;
        [Tooltip("0 : Not Select, 1 : Select")]
        [SerializeField] private Sprite[] mapIcons;

        private int roomId;
        private MapUI mapUI;

        public void Initialize(int roomId, MapUI mapUI)
        {
            this.roomId = roomId;
            this.mapUI = mapUI;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);

            roomIcon.sprite = defaultIcon;
        }

        public void SetState(bool isCurrent, bool canSelect, bool isVisited, bool isRevealed)
        {
            button.interactable = canSelect;
            transform.localScale = isCurrent ? Vector3.one * 1.2f : Vector3.one;

            background.sprite = mapIcons[isCurrent ? 1 : 0];

            clearIcon.sprite = clearSprite;
            clearIcon.enabled = isVisited && !isCurrent;

            if (!isRevealed && !isVisited)
                roomIcon.sprite = hideIcon;
            else
                SetRoomIcon();
        }

        private void SetRoomIcon()
        {
            MapUI.RoomType type = mapUI.GetRoomType(roomId);

            switch (type)
            {
                case MapUI.RoomType.Start:
                    roomIcon.sprite = defaultIcon;
                    break;

                case MapUI.RoomType.Normal:
                    roomIcon.sprite = normalIcon;
                    break;

                case MapUI.RoomType.Elite:
                    roomIcon.sprite = eliteIcon;
                    break;

                case MapUI.RoomType.Shop:
                    roomIcon.sprite = shopIcon;
                    break;

                case MapUI.RoomType.Boss:
                    roomIcon.sprite = bossIcon;
                    break;
            }
        }

        private void OnClick()
        {
            mapUI.SelectRoom(roomId);
        }
    }
}