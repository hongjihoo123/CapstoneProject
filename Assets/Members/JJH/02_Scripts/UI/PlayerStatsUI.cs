using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using Members.KYR._01.Scripts.Stats;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Members.KYR._01.Scripts.UI
{
    public class PlayerStatsUI : MonoBehaviour
    {
        private enum StatFormat
        {
            Number,         // 12.5
            Percent,        // 0.25 -> 25%
            Multiplier,     // 1.2  -> x1.2
            DefenseRating,  // 50   -> 50 (33.3%)  방어 점감 적용 경감률
            ElementRating   // 50   -> 50 (33.3%)  속성 점감 적용 경감률
        }

        private readonly struct StatEntry
        {
            public readonly PlayerStatId Id;
            public readonly string Label;
            public readonly StatFormat Format;

            public StatEntry(PlayerStatId id, string label, StatFormat format = StatFormat.Number)
            {
                Id = id;
                Label = label;
                Format = format;
            }
        }

        private readonly struct StatGroup
        {
            public readonly string Title;
            public readonly StatEntry[] Entries;

            public StatGroup(string title, StatEntry[] entries)
            {
                Title = title;
                Entries = entries;
            }
        }

        private sealed class RowBinding
        {
            public StatEntry Entry;
            public TMP_Text ValueText;
            public float LastValue = float.NaN;
        }

        [Header("Event")]
        [SerializeField] private EventChannelSO playerStatsChannel;

        [Header("UI")]
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform content;
        [SerializeField] private GameObject headerPrefab;
        [SerializeField] private GameObject rowPrefab;

        private static readonly StatGroup[] Groups =
        {
            new("공격", new[]
            {
                new StatEntry(PlayerStatId.Damage, "공격력", StatFormat.Multiplier),
                new StatEntry(PlayerStatId.AttackSpeed, "공격 속도", StatFormat.Multiplier),
                new StatEntry(PlayerStatId.ArmorPenetration, "물리 관통력"),
                new StatEntry(PlayerStatId.MagicPenetration, "마법 관통력"),
                new StatEntry(PlayerStatId.Omnivamp, "피해 흡혈", StatFormat.Percent),
                new StatEntry(PlayerStatId.SkillCooldownReduction, "스킬 쿨타임 감소", StatFormat.Percent),
                new StatEntry(PlayerStatId.FirePenetration, "화염 관통력"),
                new StatEntry(PlayerStatId.WaterPenetration, "물 관통력"),
                new StatEntry(PlayerStatId.WindPenetration, "바람 관통력"),
                new StatEntry(PlayerStatId.ElectricPenetration, "전기 관통력"),
                new StatEntry(PlayerStatId.EarthPenetration, "대지 관통력"),
            }),
            new("방어", new[]
            {
                new StatEntry(PlayerStatId.MaxHp, "최대 체력"),
                new StatEntry(PlayerStatId.DamageReduction, "피해 감소", StatFormat.Percent),
                new StatEntry(PlayerStatId.IncomingDamageTaken, "받는 피해 배율", StatFormat.Multiplier),
                new StatEntry(PlayerStatId.HealReceived, "받는 회복 배율", StatFormat.Multiplier),
                new StatEntry(PlayerStatId.Tenacity, "강인함"),
                new StatEntry(PlayerStatId.ArmorDefense, "물리 방어력", StatFormat.DefenseRating),
                new StatEntry(PlayerStatId.MagicDefense, "마법 방어력", StatFormat.ElementRating),
            }),
            new("이동", new[]
            {
                new StatEntry(PlayerStatId.MoveSpeed, "이동 속도"),
                new StatEntry(PlayerStatId.DashSpeed, "대시 속도"),
                new StatEntry(PlayerStatId.DashDuration, "대시 지속시간"),
            }),
        };

        private readonly List<RowBinding> _rows = new();
        private bool _built;

        private void Awake()
        {
            Build();
        }

        private void OnEnable()
        {
            if (playerStatsChannel == null)
            {
                Debug.LogWarning($"{name}: systemChannel이 연결되지 않았습니다.", this);
                return;
            }

            playerStatsChannel.AddListener<PlayerStatsChangedEvent>(HandleStatsChanged);
            // 켜지기 전에 발행된 값을 놓쳤을 수 있으니 현재 값을 요청
            playerStatsChannel.RaiseEvent(PlayerStatsEvents.PlayerStatsRequestEvent);
        }

        private void OnDisable()
        {
            if (playerStatsChannel != null)
                playerStatsChannel.RemoveListener<PlayerStatsChangedEvent>(HandleStatsChanged);
        }

        public void Toggle()
        {
            if (panel != null)
                panel.SetActive(!panel.activeSelf);
        }

        private void HandleStatsChanged(PlayerStatsChangedEvent evt)
        {
            if (!_built)
                return;

            foreach (RowBinding row in _rows)
            {
                float value = evt.Get(row.Entry.Id);
                if (Mathf.Approximately(value, row.LastValue))
                    continue;

                row.LastValue = value;
                row.ValueText.text = Format(row.Entry.Format, value);
            }
        }

        private void Build()
        {
            if (_built)
                return;

            if (content == null || rowPrefab == null)
            {
                Debug.LogWarning($"{name}: content 또는 rowPrefab이 연결되지 않았습니다.", this);
                return;
            }

            foreach (StatGroup group in Groups)
            {
                if (headerPrefab != null)
                {
                    GameObject header = Instantiate(headerPrefab, content);
                    TMP_Text headerText = header.GetComponentInChildren<TMP_Text>();
                    if (headerText != null)
                        headerText.text = group.Title;
                }

                foreach (StatEntry entry in group.Entries)
                {
                    GameObject row = Instantiate(rowPrefab, content);
                    TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>();
                    if (texts.Length < 2)
                    {
                        Debug.LogWarning($"{name}: rowPrefab에는 TMP_Text가 2개(이름/값) 필요합니다.", this);
                        Destroy(row);
                        continue;
                    }

                    texts[0].text = entry.Label;
                    _rows.Add(new RowBinding { Entry = entry, ValueText = texts[1] });
                }
            }

            _built = true;
        }

        private static string Format(StatFormat format, float value)
        {
            switch (format)
            {
                case StatFormat.Percent:
                    return $"{value * 100f:0.#}%";
                case StatFormat.Multiplier:
                    return $"x{value:0.##}";
                case StatFormat.DefenseRating:
                    return $"{value:0.##} ({StatFormulas.Mitigation(value, StatFormulas.DefenseK) * 100f:0.#}%)";
                case StatFormat.ElementRating:
                    return $"{value:0.##} ({StatFormulas.Mitigation(value, StatFormulas.ElementK) * 100f:0.#}%)";
                default:
                    return $"{value:0.##}";
            }
        }
    }
}