using System.Collections.Generic;
using Assets.Members.HJH._02.Scripts.Char.FSM_Skill_Module;
using Members.KYR._01.Scripts.Currency;
using Members.KYR._01.Scripts.Player;
using Members.KYR._01.Scripts.SkillUpgrade.UI;
using Members.KYR._01_Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.KYR._01.Scripts.SkillUpgrade
{
    [DefaultExecutionOrder(5000)]
    [RequireComponent(typeof(SphereCollider))]
    public class SkillUpgradeShrine : MonoBehaviour
    {
        private static readonly SkillSlotId[] SlotOrder =
        {
            SkillSlotId.Basic1, SkillSlotId.Basic2, SkillSlotId.Weapon, SkillSlotId.Dash, SkillSlotId.Ultimate
        };

        [SerializeField] private SkillUpgradeTableSO table;
        [SerializeField, Min(0), Tooltip("이 이벤트에서 강화할 수 있는 횟수. 0 = 제한 없음")]
        private int maxPurchases = 1;
        [SerializeField, Min(0.1f), Tooltip("트리거(SphereCollider) 반경")] private float detectRadius = 2.2f;

        [Header("UI")]
        [SerializeField, Tooltip("SkillUpgradePanel 프리팹. 처음 열 때 한 번 생성한다")]
        private SkillUpgradePanel panelPrefab;

        [Header("상호작용 안내")]
        [SerializeField, Tooltip("범위 안에 있을 때 켜지는 안내 루트 (카메라를 바라본다)")]
        private GameObject prompt;
        [SerializeField, Tooltip("안내 이미지. Sprite를 넣으면 이미지가 보이고 글자는 숨긴다")]
        private SpriteRenderer promptIcon;
        [SerializeField, Tooltip("이미지가 없을 때 보이는 글자")]
        private TMP_Text promptLabel;
        [SerializeField, Tooltip("켜면 글자를 입력 설정의 키 이름으로 바꾼다 (예: Hold E | Hold Y)")]
        private bool useBindingLabel;

        private readonly List<SkillSlotId> _rowSlots = new List<SkillSlotId>();
        private readonly List<SkillUpgradeRowData> _rowData = new List<SkillUpgradeRowData>();
        private SkillUpgradePanel _panel;
        private PlayerAgent _player;
        private PlayerInputSO _input;
        private int _inside;
        private bool _open;
        private int _purchases;
        private int _freeCursor;

        private bool Exhausted => maxPurchases > 0 && _purchases >= maxPurchases;

        private void Awake()
        {
            ApplyTrigger();
            if (prompt != null)
                prompt.SetActive(false);
        }

        private void OnValidate()
        {
            ApplyTrigger();
        }

        private void ApplyTrigger()
        {
            SphereCollider range = GetComponent<SphereCollider>();
            if (range == null)
                return;

            range.isTrigger = true;
            range.radius = detectRadius;
            range.center = new Vector3(0f, 0.9f, 0f);
        }

        private void OnEnable()
        {
            SkillUpgradeService.Changed += HandleChanged;
            if (GameBootstrap.Currency != null)
                GameBootstrap.Currency.OnChanged += HandleGoldChanged;
        }

        private void OnDisable()
        {
            SkillUpgradeService.Changed -= HandleChanged;
            if (GameBootstrap.Currency != null)
                GameBootstrap.Currency.OnChanged -= HandleGoldChanged;
        }

        private void OnDestroy()
        {
            UnbindInput();
            if (_open)
            {
                _open = false;
                UiFocusService.Release(this);
            }

            if (_panel != null)
                Destroy(_panel.gameObject);
        }

        private void Update()
        {
            if (_inside > 0 && (_player == null || !_player.IsAlive))
            {
                _inside = 0;
                UnbindInput();
                Close();
            }

            if (_open)
                ReadKeys();
            else
                SetPrompt(_inside > 0 && !Exhausted);
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerAgent agent = other != null ? other.GetComponentInParent<PlayerAgent>() : null;
            if (agent == null)
                return;

            _inside++;
            BindInput(agent);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerAgent agent = other != null ? other.GetComponentInParent<PlayerAgent>() : null;
            if (agent == null)
                return;

            _inside = Mathf.Max(0, _inside - 1);
            if (_inside > 0)
                return;

            UnbindInput();
            Close();
        }

        private void LateUpdate()
        {
            if (prompt != null && prompt.activeSelf && Camera.main != null)
                prompt.transform.rotation = Camera.main.transform.rotation;

            if (_open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (_freeCursor <= 0)
                return;

            _freeCursor--;
            if (!UiFocusService.IsUiFocused)
                Cursor.lockState = CursorLockMode.None;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.25f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + new Vector3(0f, 0.9f, 0f), detectRadius);
        }

        private void Open()
        {
            if (Exhausted || _player == null || _player.SkillFsm == null)
                return;

            if (_panel == null)
            {
                if (panelPrefab == null)
                {
                    Debug.LogWarning($"{name}: Panel Prefab이 연결되지 않았습니다.", this);
                    return;
                }

                _panel = Instantiate(panelPrefab);
                _panel.CloseClicked += Close;
            }

            if (table == null)
                Debug.LogWarning($"{name}: SkillUpgradeTable이 연결되지 않아 강화할 스킬이 없습니다.", this);

            _open = true;
            _panel.Show();
            SetPrompt(false);
            UiFocusService.Acquire(this);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
        }

        private void Close()
        {
            if (!_open)
                return;

            _open = false;
            if (_panel != null)
                _panel.Hide();
            UiFocusService.Release(this);
            Cursor.lockState = CursorLockMode.None;
            _freeCursor = 8;
        }

        private void Choose(int index)
        {
            if (!_open || index < 0 || index >= _rowSlots.Count || Exhausted)
                return;

            SkillData skill = _player.SkillFsm.GetSkill(_rowSlots[index]);
            if (!SkillUpgradeService.TryUpgrade(table, skill))
                return;

            _purchases++;
            if (Exhausted)
                Close();
            else
                Refresh();
        }

        private void HandleChanged(string key, int level)
        {
            if (_open)
                Refresh();
        }

        private void HandleGoldChanged(CurrencyType type, int value)
        {
            if (_open && type == CurrencyType.RunGold)
                Refresh();
        }

        private void Refresh()
        {
            if (_panel == null || _player == null || _player.SkillFsm == null)
                return;

            _rowSlots.Clear();
            _rowData.Clear();
            foreach (SkillSlotId slot in SlotOrder)
            {
                SkillData skill = _player.SkillFsm.GetSkill(slot);
                if (skill == null || skill is EmptySkillData)
                    continue;

                _rowSlots.Add(slot);
                _rowData.Add(Describe(_rowData.Count, slot, skill));
            }

            int gold = GameBootstrap.Currency != null ? GameBootstrap.Currency.Get(CurrencyType.RunGold) : 0;
            _panel.SetHeader("스킬 강화", gold, maxPurchases > 0 ? maxPurchases - _purchases : -1);
            _panel.SetRows(_rowData, Choose);
        }

        private SkillUpgradeRowData Describe(int index, SkillSlotId slot, SkillData skill)
        {
            int level = SkillUpgradeService.GetLevel(skill);
            SkillUpgradeResult state = SkillUpgradeService.CanUpgrade(table, skill);
            SkillUpgradeTableSO.Level next = SkillUpgradeService.NextLevel(table, skill);
            string now = level > 0
                ? SkillUpgradeService.Describe(SkillUpgradeService.GetFlatBonus(skill), SkillUpgradeService.GetPercentBonus(skill))
                : "기본";

            var data = new SkillUpgradeRowData
            {
                Icon = skill.Icon,
                Key = $"{index + 1}  {KeyLabel(slot)}",
                Name = skill.DisplayName,
                Level = $"Lv {level}",
                State = state
            };

            switch (state)
            {
                case SkillUpgradeResult.Ok:
                case SkillUpgradeResult.NotEnoughGold:
                    data.Effect = $"{now}  →  {SkillUpgradeService.Describe(next.flatDamage, next.percentDamage)}";
                    data.Cost = $"{next.cost} G";
                    break;
                case SkillUpgradeResult.MaxLevel:
                    data.Effect = now;
                    data.Cost = "최대";
                    break;
                default:
                    data.Effect = "강화할 수 없는 스킬";
                    data.Cost = "-";
                    break;
            }

            return data;
        }

        private static string KeyLabel(SkillSlotId slot)
        {
            switch (slot)
            {
                case SkillSlotId.Basic1: return "Q";
                case SkillSlotId.Basic2: return "E";
                case SkillSlotId.Weapon: return "우클릭";
                case SkillSlotId.Dash: return "Space";
                case SkillSlotId.Ultimate: return "R";
                default: return slot.ToString();
            }
        }

        private void ReadKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            for (int i = 0; i < 5; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    Choose(i);
                    return;
                }
            }
        }

        private void BindInput(PlayerAgent agent)
        {
            if (agent == null || (_player == agent && _input != null))
                return;

            UnbindInput();
            _player = agent;
            _input = agent.PlayerInput;
            if (_input != null)
                _input.OnInteractPressed += HandleInteract;
        }

        private void UnbindInput()
        {
            if (_input != null)
                _input.OnInteractPressed -= HandleInteract;
            _input = null;
        }

        private void HandleInteract()
        {
            if (_player == null || !_player.IsAlive)
                return;

            if (_open)
                Close();
            else if (_inside > 0)
                Open();
        }

        private void SetPrompt(bool visible)
        {
            if (prompt == null || prompt.activeSelf == visible)
                return;

            prompt.SetActive(visible);
            if (!visible)
                return;
            
            bool hasIcon = promptIcon != null && promptIcon.sprite != null;
            if (promptIcon != null)
                promptIcon.enabled = hasIcon;
            if (promptLabel != null)
            {
                promptLabel.gameObject.SetActive(!hasIcon);
                if (!hasIcon && useBindingLabel && _input != null)
                    promptLabel.text = _input.InteractBindingLabel;
            }
        }
    }
}
