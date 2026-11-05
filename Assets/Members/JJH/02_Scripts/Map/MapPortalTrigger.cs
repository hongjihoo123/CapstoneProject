using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.JJH._02_Scripts.Map
{
    public class MapPortalTrigger : MonoBehaviour
    {
        [SerializeField] private EventChannelSO mapChannel;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private GameObject keyImage;

        private bool isPlayerInRange;

        private void Awake()
        {
            if (keyImage != null)
                keyImage.SetActive(false);
        }

        private void Update()
        {
            if (!isPlayerInRange || Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame)
                return;

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