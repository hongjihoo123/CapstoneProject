
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Members.JJH._02_Scripts.Map
{
    public class MapUI : MonoBehaviour
    {
        public enum RoomType
        {
            Normal,
            Elite,
            Upgrade,
            Shop,
            Boss
        }

        [Serializable]
        public struct RoomOption
        {
            public RoomType type;
            public string displayName;
            public Sprite backgroundSprite;
            public string sceneName;
            [Min(1)] public int weight;
        }

        private const int NormalRoomCount = 6;
        private const int MinChoiceCount = 2;
        private const int MaxChoiceCount = 3;

        [Header("UI")]
        [SerializeField] private Transform choiceContent;
        [SerializeField] private CanvasGroup mapUICanvasGroup;
        [SerializeField] private Image[] progressSlots;

        [Header("Progress Outline")]
        [SerializeField] private Outline[] progressOutlines;
        [SerializeField] private Color currentOutlineColor;
        [SerializeField] private Vector2 outlineEffectDistance;

        [Header("Sprites")]
        [SerializeField] private Sprite[] progressSprites;

        [Header("Room Options")]
        [SerializeField] private RoomOption[] roomOptions;
        [SerializeField] private RoomOption bossRoom;

        [Header("Data")]
        [SerializeField] private EventChannelSO eventChannel;
        [SerializeField] private MapChoiceUI choicePrefab;

        private readonly List<MapChoiceUI> _choices = new();

        private int _currentRoomCount = 1;
        private int _stageNumber = 1;

        private bool _isMapVisible;
        private bool _choicesGenerated;
        private bool _bossEntered;

        private void Awake()
        {
            DontDestroyOnLoad(transform.parent != null ? transform.parent.gameObject : gameObject);
        }

        private void OnEnable()
        {
            eventChannel.AddListener<ToggleMapEvent>(OnToggleMap);
            eventChannel.AddListener<SelectRoomEvent>(OnSelectRoom);
        }

        private void OnDisable()
        {
            eventChannel.RemoveListener<ToggleMapEvent>(OnToggleMap);
            eventChannel.RemoveListener<SelectRoomEvent>(OnSelectRoom);
        }

        private void Start()
        {
            SetMapVisible(false);
            UpdateProgressUI();
        }

        private void OnToggleMap(ToggleMapEvent evt)
        {
            bool visible = !_isMapVisible;

            if (visible && !_choicesGenerated)
                GenerateChoices();

            SetMapVisible(visible);
        }

        private void OnSelectRoom(SelectRoomEvent evt)
        {
            SelectRoom(evt.Choice, evt.RoomOption);
        }

        private void SetMapVisible(bool visible)
        {
            _isMapVisible = visible;
            mapUICanvasGroup.alpha = visible ? 1f : 0f;
            mapUICanvasGroup.interactable = visible;
            mapUICanvasGroup.blocksRaycasts = visible;
        }

        private void GenerateChoices()
        {
            ClearChoices();

            if (_bossEntered)
                return;

            if (_currentRoomCount >= NormalRoomCount)
            {
                CreateChoice(bossRoom);
                _choicesGenerated = true;
                return;
            }

            if (roomOptions == null || roomOptions.Length == 0)
            {
                Debug.LogError($"{gameObject.name} : Room Option 설정 안됨", this);
                return;
            }

            List<RoomOption> combatOptions = new();
            List<RoomOption> specialOptions = new();

            foreach (RoomOption option in roomOptions)
            {
                if (option.weight <= 0)
                    continue;

                if (option.type == RoomType.Normal || option.type == RoomType.Elite)
                    combatOptions.Add(option);
                else if (option.type == RoomType.Shop || option.type == RoomType.Upgrade)
                    specialOptions.Add(option);
            }

            if (combatOptions.Count == 0)
            {
                Debug.LogError($"{gameObject.name} : Normal 또는 Elite 방 설정이 필요합니다.", this);
                return;
            }

            int choiceCount = Random.Range(MinChoiceCount, MaxChoiceCount + 1);

            if (_currentRoomCount == NormalRoomCount - 1)
            {
                if (!TryGetRoomOption(RoomType.Shop, out RoomOption shopOption) ||
                    !TryGetRoomOption(RoomType.Upgrade, out RoomOption upgradeOption))
                {
                    Debug.LogError($"{gameObject.name} : Shop과 Upgrade 방 설정이 필요합니다.", this);
                    return;
                }

                CreateChoice(shopOption);
                CreateChoice(upgradeOption);

                if (choiceCount == 3)
                    CreateChoice(GetWeightedRandomOption(combatOptions));
            }
            else if (_currentRoomCount == 3)
            {
                if (specialOptions.Count == 0)
                {
                    Debug.LogError($"{gameObject.name} : Shop 또는 Upgrade 방 설정이 필요합니다.", this);
                    return;
                }

                CreateChoice(GetWeightedRandomOption(specialOptions));

                while (_choices.Count < choiceCount)
                    CreateChoice(GetWeightedRandomOption(combatOptions));
            }
            else
            {
                while (_choices.Count < choiceCount)
                    CreateChoice(GetWeightedRandomOption(combatOptions));
            }

            _choicesGenerated = _choices.Count > 0;
        }

        private RoomOption GetWeightedRandomOption(List<RoomOption> options)
        {
            int totalWeight = 0;

            foreach (RoomOption option in options)
                totalWeight += option.weight;

            int randomValue = Random.Range(0, totalWeight);

            foreach (RoomOption option in options)
            {
                randomValue -= option.weight;

                if (randomValue < 0)
                    return option;
            }

            return options[options.Count - 1];
        }

        private bool TryGetRoomOption(RoomType type, out RoomOption selectedOption)
        {
            List<RoomOption> options = new();

            foreach (RoomOption option in roomOptions)
            {
                if (option.type == type && option.weight > 0)
                    options.Add(option);
            }

            if (options.Count == 0)
            {
                selectedOption = default;
                return false;
            }

            selectedOption = GetWeightedRandomOption(options);
            return true;
        }

        private void CreateChoice(RoomOption option)
        {
            MapChoiceUI choice = Instantiate(choicePrefab, choiceContent);
            choice.Initialize(option);
            _choices.Add(choice);
        }

        private void ClearChoices()
        {
            foreach (MapChoiceUI choice in _choices)
            {
                if (choice != null)
                    Destroy(choice.gameObject);
            }

            _choices.Clear();
        }

        public void SelectRoom(MapChoiceUI choice, RoomOption option)
        {
            if (choice == null || !_choices.Contains(choice))
                return;

            if (_bossEntered)
                return;

            if (_currentRoomCount >= NormalRoomCount)
            {
                if (option.type != RoomType.Boss)
                    return;

                _bossEntered = true;
            }
            else
            {
                if (option.type == RoomType.Boss)
                    return;

                _currentRoomCount++;
            }

            UpdateProgressUI();
            _choicesGenerated = false;
            SetMapVisible(false);
            ClearChoices();

            if (string.IsNullOrWhiteSpace(option.sceneName))
            {
                Debug.LogError($"{option.displayName}의 Scene Name이 설정되지 않았습니다.", this);
                return;
            }

            eventChannel.RaiseEvent(SystemEvents.ChangeSceneEvent.Init(option.sceneName));
        }

        private void UpdateProgressUI()
        {
            if (progressSlots == null || progressSlots.Length < 7 ||
                progressSprites == null || progressSprites.Length < 3)
            {
                Debug.LogError($"{gameObject.name} : Progress Slots는 7개, Progress Sprites는 3개가 필요합니다.", this);
                return;
            }

            for (int i = 0; i < NormalRoomCount; i++)
            {
                progressSlots[i].sprite = progressSprites[i < _currentRoomCount ? 0 : 1];

                if (progressOutlines != null && i < progressOutlines.Length && progressOutlines[i] != null)
                {
                    progressOutlines[i].effectColor = currentOutlineColor;
                    progressOutlines[i].effectDistance = outlineEffectDistance;
                    progressOutlines[i].enabled = i == _currentRoomCount - 1;
                }
            }

            progressSlots[6].sprite = progressSprites[2];

            if (progressOutlines != null && progressOutlines.Length > 6 && progressOutlines[6] != null)
            {
                progressOutlines[6].effectColor = currentOutlineColor;
                progressOutlines[6].effectDistance = outlineEffectDistance;
                progressOutlines[6].enabled = _bossEntered;
            }
        }

        public void CompleteBossStage()
        {
            if (!_bossEntered)
                return;

            _stageNumber++;
            _currentRoomCount = 1;
            _bossEntered = false;
            _choicesGenerated = false;

            ClearChoices();
            UpdateProgressUI();
            SetMapVisible(false);
        }

        public int GetStageNumber()
        {
            return _stageNumber;
        }

        public int GetCurrentRoomCount()
        {
            return _currentRoomCount;
        }
    }
}
