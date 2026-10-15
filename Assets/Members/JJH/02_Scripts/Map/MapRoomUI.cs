using UnityEngine;
using UnityEngine.UI;

namespace Members.JJH._02_Scripts.Map
{
    public class MapRoomUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button button;
        [SerializeField] private Image icon;

        [Header("Icons")]
        [SerializeField] private Sprite defaultIcon;
        [SerializeField] private Sprite startIcon;
        [SerializeField] private Sprite normalIcon;
        [SerializeField] private Sprite eliteIcon;
        [SerializeField] private Sprite eventIcon;
        [SerializeField] private Sprite shopIcon;
        [SerializeField] private Sprite bossIcon;

        private int roomId;
        private MapUI mapUI;

        public void Initialize(int roomId, MapUI mapUI)
        {
            this.roomId = roomId;
            this.mapUI = mapUI;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);

            icon.sprite = defaultIcon;
        }

        public void SetState(bool isCurrent, bool canSelect, bool isVisited, bool isRevealed)
        {
            button.interactable = canSelect;
            transform.localScale = isCurrent ? Vector3.one * 1.2f : Vector3.one;

            if (isRevealed || isVisited)
                SetRoomIcon();
            else
                icon.sprite = defaultIcon;
        }

        private void SetRoomIcon()
        {
            MapUI.RoomType type = mapUI.GetRoomType(roomId);

            switch (type)
            {
                case MapUI.RoomType.Start:
                    icon.sprite = startIcon;
                    break;

                case MapUI.RoomType.Normal:
                    icon.sprite = normalIcon;
                    break;

                case MapUI.RoomType.Elite:
                    icon.sprite = eliteIcon;
                    break;

                case MapUI.RoomType.Event:
                    icon.sprite = eventIcon;
                    break;

                case MapUI.RoomType.Shop:
                    icon.sprite = shopIcon;
                    break;

                case MapUI.RoomType.Boss:
                    icon.sprite = bossIcon;
                    break;
            }
        }

        private void OnClick()
        {
            mapUI.SelectRoom(roomId);
        }
    }
}