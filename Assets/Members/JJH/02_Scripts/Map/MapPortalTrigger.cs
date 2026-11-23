using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using Members.KYR._01_Scripts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.JJH._02_Scripts.Map
{
    public class MapPortalTrigger : MonoBehaviour
    {
        [SerializeField] private PlayerInputSO playerInput;
        [SerializeField] private EventChannelSO mapChannel;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private GameObject keyImage;

        private bool isPlayerInRange;
        private bool isMapOpen;

        private void Awake()
        {
            if (keyImage != null)
                keyImage.SetActive(false);
        }

        private void Update()
        {
            if (!isPlayerInRange || Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame)
                return;

            // 맵 UI 가 없으면 입력만 막혀 플레이어가 멈춘다 → 막지 않고 알린다 (보통은 MapSystemBootstrap 이 만들어 둠)
            if (mapChannel == null || !mapChannel.HasListener<ToggleMapEvent>())
            {
                Debug.LogWarning($"[MapPortal] {name}: 맵 UI(Map_Canvas)가 없습니다. Portal 의 MapSystemBootstrap 에 Map_Canvas 프리팹이 연결됐는지 확인하세요.", this);
                return;
            }

            isMapOpen = !isMapOpen;
            playerInput.SetMoveAndClickInput(!isMapOpen);
            mapChannel.RaiseEvent(MapEvents.ToggleMapEvent);
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((playerLayer.value & (1 << other.gameObject.layer)) == 0)
                return;

            isPlayerInRange = true;
            if (keyImage != null)
                keyImage.SetActive(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if ((playerLayer.value & (1 << other.gameObject.layer)) == 0)
                return;

            isPlayerInRange = false;
            if (keyImage != null)
                keyImage.SetActive(false);
        }
    }
}