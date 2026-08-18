using MemberWorkSpace.JJH._02_Scripts.Systems.ObjectPoolSystem.Runtime;
using System;
using UnityEngine.UIElements;

namespace MemberWorkSpace.JJH._02_Scripts.Systems.ObjectPoolSystem.Editor
{
    public class PoolItemView
    {
        private Label _nameLabel;
        private Label _warningLabel;
        private Button _deleteBtn;
        private VisualElement _rootElement;

        public event Action<PoolItemView> OnDeleteEvent;
        public event Action<PoolItemView> OnSelectEvent;

        public string Name
        {
            get => _nameLabel.text;
            set => _nameLabel.text = value;
        }

        public PoolItemSO TargetItem { get; private set; }

        public bool IsActive
        {
            get => _rootElement.ClassListContains("active");
            set => _rootElement.EnableInClassList("active", value);
        }

        public bool IsEmpty
        {
            get => _warningLabel.ClassListContains("on");
            set => _warningLabel.EnableInClassList("on", value);
        }

        public PoolItemView(VisualElement rootElement, PoolItemSO targetItem)
        {
            TargetItem = targetItem;
            _rootElement = rootElement.Q("PoolItem");
            _nameLabel = _rootElement.Q<Label>("ItemName");
            _deleteBtn = _rootElement.Q<Button>("DeleteBtn");
            _warningLabel = _rootElement.Q<Label>("WarningLabel");

            _deleteBtn.RegisterCallback<ClickEvent>(evt =>
            {
                OnDeleteEvent?.Invoke(this);
                evt.StopPropagation();
            });

            _rootElement.RegisterCallback<ClickEvent>(evt =>
            {
                OnSelectEvent?.Invoke(this);
                evt.StopPropagation();
            });
        }

    }
}