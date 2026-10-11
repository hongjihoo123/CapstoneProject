using Members.JJH._02_Scripts.ElementsSystem;
using System.Collections.Generic;

namespace Members.JJH._02_Scripts.Systems.EventChannelSystem.Events
{
    public static class SystemEvents
    {
        public static readonly UseSkillEvent UseSkillEvent = new();
        public static readonly ElementStackChangedEvent ElementStackChangedEvent = new();
        public static readonly ElementBuffTriggeredEvent ElementBuffTriggeredEvent = new();
        public static readonly ElementComboNextEvent ElementComboNextEvent = new();
        public static readonly ElementComboCompleteEvent ElementComboCompleteEvent = new();
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

    public class ElementComboNextEvent : GameEvent
    {
        public ElementType NextElement { get; private set; }
        public string ComboName { get; private set; }
        public int Progress { get; private set; }
        public int ComboLength { get; private set; }

        public ElementComboNextEvent Init(ElementType nextElement, string comboName, int progress, int comboLength)
        {
            NextElement = nextElement;
            ComboName = comboName;
            Progress = progress;
            ComboLength = comboLength;
            return this;
        }
    }

    public class ElementComboCompleteEvent : GameEvent
    {
        public string ComboName { get; private set; }
        public IReadOnlyList<ElementType> Sequence { get; private set; }

        public ElementComboCompleteEvent Init(string comboName, IReadOnlyList<ElementType> sequence)
        {
            ComboName = comboName;
            Sequence = sequence;
            return this;
        }
    }

    #endregion
}