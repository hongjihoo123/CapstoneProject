using Members.JJH._02_Scripts.ElementsSystem;
using System.Collections.Generic;

namespace Members.JJH._02_Scripts.Systems.EventChannelSystem.Events
{
    public static class SystemEvents
    {
        public static readonly UseSkillEvent UseSkillEvent = new UseSkillEvent();
        public static readonly ElementStackChangedEvent ElementStackChangedEvent = new ElementStackChangedEvent();
        public static readonly ElementBuffTriggeredEvent ElementBuffTriggeredEvent = new ElementBuffTriggeredEvent();
    }

    #region Element System Events

    /// <summary>
    /// 스킬 사용 시 발생
    /// </summary>
    public class UseSkillEvent : GameEvent
    {
        public ElementType Type;

        public UseSkillEvent Init(ElementType type)
        {
            Type = type;
            return this;
        }
    }

    /// <summary>스택 목록이 바뀔 때마다 발생. UI가 구독한다.</summary>
    public class ElementStackChangedEvent : GameEvent
    {
        public IReadOnlyList<ElementType> Stacks;
        public int MaxStack;
        public float TimerDuration; // 스택 효과 자동 발생까지 남은 시간

        public ElementStackChangedEvent Init(IReadOnlyList<ElementType> stacks, int maxStack, float timerDuration)
        {
            Stacks = stacks;
            MaxStack = maxStack;
            TimerDuration = timerDuration;
            return this;
        }
    }

    /// <summary>
    /// 스택 효과 발동 시 발생
    /// </summary>
    public class ElementBuffTriggeredEvent : GameEvent
    {
        public IReadOnlyList<ElementType> Stacks;

        public ElementBuffTriggeredEvent Init(IReadOnlyList<ElementType> stacks)
        {
            Stacks = stacks;
            return this;
        }
    }

    #endregion
}