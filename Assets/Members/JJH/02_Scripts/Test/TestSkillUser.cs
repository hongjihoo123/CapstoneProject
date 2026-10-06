using Members.JJH._02_Scripts.ElementsSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.JJH._02_Scripts.Test
{
    public class TestSkillUser : MonoBehaviour
    {
        [SerializeField] private EventChannelSO systemChannel;

        private void Update()
        {
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                systemChannel.RaiseEvent(SystemEvents.UseSkillEvent.Init(ElementType.Fire));
            }
            else if (Keyboard.current.wKey.wasPressedThisFrame)
            {
                systemChannel.RaiseEvent(SystemEvents.UseSkillEvent.Init(ElementType.Water));
            }
            else if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                systemChannel.RaiseEvent(SystemEvents.UseSkillEvent.Init(ElementType.Wind));
            }
            else if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                systemChannel.RaiseEvent(SystemEvents.UseSkillEvent.Init(ElementType.Electric));
            }
            else if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                systemChannel.RaiseEvent(SystemEvents.UseSkillEvent.Init(ElementType.Earth));
            }
        }
    }
}