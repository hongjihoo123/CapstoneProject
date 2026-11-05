using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using System.Collections.Generic;
using UnityEngine;

namespace Members.JJH._02_Scripts.ElementsSystem
{
    public enum ElementType
    {
        Fire,
        Water,
        Wind,
        Electric,
        Earth
    }

    public class ElementStackManager : MonoBehaviour
    {
        [SerializeField] private EventChannelSO elementsChannel;
        [SerializeField, Min(1)] private int maxStack = 4;
        [SerializeField, Min(0.1f)] private float triggerDelay = 3f;

        private ElementStackModel _model;
        private float _timer;
        private bool _timerRunning;

        private void Awake()
        {
            _model = new ElementStackModel(maxStack);
        }

        private void OnEnable()
        {
            elementsChannel.AddListener<UseSkillEvent>(HandleUseSkill);
            elementsChannel.AddListener<ElementBuffTriggeredEvent>(HandleBuffTriggered);
        }

        private void OnDisable()
        {
            elementsChannel.RemoveListener<UseSkillEvent>(HandleUseSkill);
            elementsChannel.RemoveListener<ElementBuffTriggeredEvent>(HandleBuffTriggered);
        }

        private void Update()
        {
            if (!_timerRunning)
                return;

            _timer -= Time.deltaTime;

            if (_timer <= 0f)
                ResetStacks();
        }

        private void HandleUseSkill(UseSkillEvent evt)
        {
            if (!_model.TryPush(evt.Type))
                return;

            _timer = triggerDelay;
            _timerRunning = true;

            RaiseStackChanged(triggerDelay);

            if (_model.IsFull)
            {
                List<ElementType> stacks = new List<ElementType>(_model.Stacks);
                elementsChannel.RaiseEvent(ElementEvents.ElementBuffTriggeredEvent.Init(stacks));
            }
        }

        private void HandleBuffTriggered(ElementBuffTriggeredEvent evt)
        {
            _timer = triggerDelay;
            _timerRunning = true;
        }

        private void ResetStacks()
        {
            _timerRunning = false;
            _timer = 0f;

            _model.Clear();

            RaiseStackChanged(0f);
        }

        private void RaiseStackChanged(float timerDuration)
        {
            elementsChannel.RaiseEvent(ElementEvents.ElementStackChangedEvent.Init(_model.Stacks, _model.MaxStack, timerDuration));
        }
    }
}