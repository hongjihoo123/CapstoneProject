using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
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
        [SerializeField] private EventChannelSO systemChannel;
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
            systemChannel.AddListener<UseSkillEvent>(HandleUseSkill);
        }

        private void OnDisable()
        {
            systemChannel.RemoveListener<UseSkillEvent>(HandleUseSkill);
        }

        private void Update()
        {
            if (!_timerRunning) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                StackTrigger();
        }

        private void HandleUseSkill(UseSkillEvent evt)
        {
            if (!_model.TryPush(evt.Type)) return;

            // 최대 스택 달성 시 즉시 스택 효과 실행
            if (_model.IsFull)
            {
                _timerRunning = false;
                RaiseStackChanged(0f);
                StackTrigger();
                return;
            }

            // 아닐 경우 타이머 실행
            _timer = triggerDelay;
            _timerRunning = true;
            RaiseStackChanged(triggerDelay);
        }

        private void StackTrigger()
        {
            _timerRunning = false;

            systemChannel.RaiseEvent(SystemEvents.ElementBuffTriggeredEvent.Init(_model.Stacks));

            _model.Clear();

            RaiseStackChanged(0f);
        }

        private void RaiseStackChanged(float timerDuration)
        {
            systemChannel.RaiseEvent(SystemEvents.ElementStackChangedEvent
                                                            .Init(_model.Stacks, _model.MaxStack, timerDuration));
        }
    }
}