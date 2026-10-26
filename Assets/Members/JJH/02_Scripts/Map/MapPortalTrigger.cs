using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.JJH._02_Scripts.Map
{
    public class MapPortalTrigger : MonoBehaviour
    {
        [SerializeField] private EventChannelSO eventChannel;
        [SerializeField] private LayerMask playerLayer;

        private bool isPlayerInRange;

        private void Update()
        {
            if (!isPlayerInRange || Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame)
                return;

            eventChannel.RaiseEvent(SystemEvents.ToggleMapEvent);
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((playerLayer.value & (1 << other.gameObject.layer)) != 0)
                isPlayerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if ((playerLayer.value & (1 << other.gameObject.layer)) != 0)
                isPlayerInRange = false;
        }
    }
}