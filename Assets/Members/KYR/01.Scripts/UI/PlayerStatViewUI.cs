using System.Collections.Generic;
using Members.KYR._01.Scripts.Player;
using Members.KYR._01.Scripts.Stats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Members.KYR._01.Scripts.UI
{
    public class PlayerStatViewUI : MonoBehaviour
    {
        private static readonly (PlayerStatId id, string label, bool percent)[] Entries =
        {
            (PlayerStatId.Damage, "공격력", false),
            (PlayerStatId.AttackSpeed, "공격 속도", false),
            (PlayerStatId.ArmorPenetration, "물리 관통력", false),
            (PlayerStatId.MagicPenetration, "마법 관통력", false),
            (PlayerStatId.Omnivamp, "피해 흡혈", true),
            (PlayerStatId.SkillCooldownReduction, "스킬 쿨타임 감소", true),
            (PlayerStatId.FirePenetration, "화염 관통력", false),
            (PlayerStatId.WaterPenetration, "물 관통력", false),
            (PlayerStatId.WindPenetration, "바람 관통력", false),
            (PlayerStatId.ElectricPenetration, "전기 관통력", false),
            (PlayerStatId.EarthPenetration, "대지 관통력", false),

            (PlayerStatId.MaxHp, "최대 체력", false),
            (PlayerStatId.DamageReduction, "피해 감소", true),
            (PlayerStatId.IncomingDamageTaken, "받는 피해 배율", false),
            (PlayerStatId.HealReceived, "받는 회복 배율", false),
            (PlayerStatId.Tenacity, "강인함", false),
            (PlayerStatId.ArmorDefense, "물리 방어력", false),
            (PlayerStatId.MagicDefense, "마법 방어력", false),

            (PlayerStatId.MoveSpeed, "이동 속도", false),
        };

        [SerializeField] private StatRowUI rowPrefab;
        [SerializeField] private Transform rowParent;
        [SerializeField] private GameObject panel;
        [SerializeField] private Key toggleKey = Key.Tab;
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        private readonly List<StatRowUI> _rows = new();
        private PlayerStatTree _tree;
        private float _timer;

        private void Start()
        {
            if (_tree == null)
                TryBindPlayer();
        }

        public void Bind(PlayerStatTree tree)
        {
            _tree = tree;

            foreach (StatRowUI row in _rows)
                Destroy(row.gameObject);
            _rows.Clear();

            if (tree == null || rowPrefab == null || rowParent == null)
                return;

            for (int i = 0; i < Entries.Length; i++)
                _rows.Add(Instantiate(rowPrefab, rowParent));

            Refresh();
        }

        private void OnEnable()
        {
            _timer = 0f;
            if (_tree != null && _rows.Count == Entries.Length)
                Refresh();
        }

        private void Update()
        {
            if (WasTogglePressed() && panel != null)
            {
                panel.SetActive(!panel.activeSelf);
                if (panel.activeSelf && _tree != null && _rows.Count == Entries.Length)
                    Refresh();
            }

            if (_tree == null)
            {
                TryBindPlayer();
                return;
            }

            if (panel != null && !panel.activeSelf)
                return;

            if (_rows.Count != Entries.Length)
                return;

            _timer += Time.unscaledDeltaTime;
            if (_timer < refreshInterval)
                return;

            _timer = 0f;
            Refresh();
        }

        private void TryBindPlayer()
        {
            PlayerAgent player = FindFirstObjectByType<PlayerAgent>();
            if (player == null || player.Stats == null || player.Stats.Tree == null)
                return;

            Bind(player.Stats.Tree);
        }

        private bool WasTogglePressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[toggleKey].wasPressedThisFrame;
        }

        private void Refresh()
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                var (id, label, percent) = Entries[i];
                float v = _tree.Get(id);
                _rows[i].Set(label, percent ? $"{v * 100f:0.#}%" : $"{v:0.##}");
            }
        }
    }
}
