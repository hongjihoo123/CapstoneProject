using Members.JJH._02_Scripts.ElementsSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module
{
    // Forwards every cast skill's element to JJH's element stack system (UseSkillEvent).
    // The skill system itself stays unaware of elements beyond the field on SkillData.
    public class SkillElementBridge : MonoBehaviour
    {
        [SerializeField] private SkillStateModule skillModule;
        [SerializeField] private EventChannelSO systemChannel;

        private void Awake()
        {
            if (skillModule == null)
                skillModule = GetComponentInChildren<SkillStateModule>();

            Debug.Assert(skillModule != null, $"{name}: SkillElementBridge needs a SkillStateModule.");
            Debug.Assert(systemChannel != null, $"{name}: SkillElementBridge needs the System event channel.");
        }

        private void OnEnable()
        {
            if (skillModule != null)
                skillModule.SkillUsed += HandleSkillUsed;
        }

        private void OnDisable()
        {
            if (skillModule != null)
                skillModule.SkillUsed -= HandleSkillUsed;
        }

        private void HandleSkillUsed(SkillUsedInfo info)
        {
            if (systemChannel == null || info.Data == null || !info.Data.TryGetElement(out ElementType element))
                return;

            systemChannel.RaiseEvent(SystemEvents.UseSkillEvent.Init(element));
        }
    }
}
