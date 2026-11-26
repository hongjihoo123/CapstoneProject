using Assets.Members.HJH._02.Scripts.Element.Upgrade;
using Assets.Members.HJH._02.Scripts.Hud;
using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using Members.KYR._01.Scripts.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    public class StatsMiniHud : MonoBehaviour, IUiBake
    {
        private enum Fmt { Number, Percent, Multiplier, DefenseRating, ElementRating, CritMultiplier }

        private readonly struct Row
        {
            public readonly PlayerStatId Id;
            public readonly string Label;
            public readonly Fmt Format;
            public readonly bool Key;
            public Row(PlayerStatId id, string label, Fmt format, bool key = false) { Id = id; Label = label; Format = format; Key = key; }
        }

        private static readonly string[] GroupTitles = { "공격", "방어", "이동" };
        private static readonly int[] GroupStart = { 0, 14, 21 };

        private static readonly Row[] Rows =
        {
            new(PlayerStatId.Damage, "공격력", Fmt.Multiplier, true),
            new(PlayerStatId.MagicDamage, "마법 공격력", Fmt.Multiplier, true),
            new(PlayerStatId.AttackSpeed, "공격 속도", Fmt.Multiplier, true),
            new(PlayerStatId.CritChance, "치명타 확률", Fmt.Percent, true),
            new(PlayerStatId.CritDamage, "치명타 피해", Fmt.CritMultiplier, true),
            new(PlayerStatId.ArmorPenetration, "물리 관통력", Fmt.Number),
            new(PlayerStatId.MagicPenetration, "마법 관통력", Fmt.Number),
            new(PlayerStatId.Omnivamp, "피해 흡혈", Fmt.Percent),
            new(PlayerStatId.SkillCooldownReduction, "스킬 쿨타임 감소", Fmt.Percent),
            new(PlayerStatId.FirePenetration, "화염 관통력", Fmt.Number),
            new(PlayerStatId.WaterPenetration, "물 관통력", Fmt.Number),
            new(PlayerStatId.WindPenetration, "바람 관통력", Fmt.Number),
            new(PlayerStatId.ElectricPenetration, "전기 관통력", Fmt.Number),
            new(PlayerStatId.EarthPenetration, "대지 관통력", Fmt.Number),
            new(PlayerStatId.MaxHp, "최대 체력", Fmt.Number, true),
            new(PlayerStatId.DamageReduction, "피해 감소", Fmt.Percent),
            new(PlayerStatId.IncomingDamageTaken, "받는 피해 배율", Fmt.Multiplier),
            new(PlayerStatId.HealReceived, "받는 회복 배율", Fmt.Multiplier),
            new(PlayerStatId.Tenacity, "강인함", Fmt.Number),
            new(PlayerStatId.ArmorDefense, "물리 방어력", Fmt.DefenseRating, true),
            new(PlayerStatId.MagicDefense, "마법 방어력", Fmt.ElementRating, true),
            new(PlayerStatId.MoveSpeed, "이동 속도", Fmt.Number),
            new(PlayerStatId.DashSpeed, "대시 속도", Fmt.Number),
            new(PlayerStatId.DashDuration, "대시 지속시간", Fmt.Number),
        };

        [SerializeField] private EventChannelSO playerStatsChannel;
        [SerializeField] private Key expandKey = Key.LeftCtrl;

        [SerializeField, Tooltip("Rows 순서대로 한 줄씩")] private GameObject[] rowObjects;
        [SerializeField, Tooltip("Rows 순서대로 값 글자")] private TMP_Text[] values;
        [SerializeField, Tooltip("공격/방어/이동 제목 줄 (펼쳤을 때만)")] private GameObject[] groupObjects;
        [SerializeField, Tooltip("Ctrl 전체 보기 안내 (접혔을 때만)")] private GameObject hint;

        private bool _expanded;
        private bool _listening;

        private void Awake() => Apply(false);

        private void Update()
        {
            bool held = Keyboard.current != null && Keyboard.current[expandKey].isPressed;
#if UNITY_EDITOR
            held |= UnityEditor.EditorPrefs.GetBool("HJH.StatsHud.ForceExpand", false);
#endif
            if (held != _expanded)
                Apply(held);

            if (_hasLast && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.3f;
                OnStats(_last);
            }
        }

        private PlayerStatsChangedEvent _last;
        private bool _hasLast;
        private float _nextRefresh;
        private int[] _map;

        private int[] Map()
        {
            int count = rowObjects != null ? rowObjects.Length : 0;
            if (_map != null && _map.Length == count)
                return _map;

            _map = new int[count];
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                _map[i] = -1;
                string n = rowObjects[i] != null ? rowObjects[i].name : string.Empty;
                for (int r = 0; r < Rows.Length; r++)
                {
                    if (n == "Row_" + Rows[r].Label)
                    {
                        _map[i] = r;
                        any = true;
                        break;
                    }
                }
            }

            if (!any && count == Rows.Length)
            {
                for (int i = 0; i < count; i++)
                    _map[i] = i;
            }
            return _map;
        }

        private static float ValueOf(Row row, PlayerStatsChangedEvent evt)
        {
            float v = evt.Get(row.Id);
            if (row.Id == PlayerStatId.CritChance)
                return v + SkillUpgrades.Bonus(SkillUpgradeKind.CritChance, SkillUpgradeScope.Attack);
            if (row.Id == PlayerStatId.CritDamage)
                return CritSystem.BaseMultiplier + v + SkillUpgrades.Bonus(SkillUpgradeKind.CritDamage, SkillUpgradeScope.Attack);
            return v;
        }

        private void Apply(bool expanded)
        {
            _expanded = expanded;
            if (rowObjects == null)
                return;
            int[] map = Map();
            for (int i = 0; i < rowObjects.Length; i++)
            {
                if (rowObjects[i] != null)
                    rowObjects[i].SetActive(expanded || (map[i] >= 0 && Rows[map[i]].Key));
            }
            if (groupObjects != null)
            {
                foreach (GameObject group in groupObjects)
                {
                    if (group != null)
                        group.SetActive(expanded);
                }
            }
            if (hint != null)
                hint.SetActive(!expanded);
        }

        public bool IsBakeStale
        {
            get
            {
                if (rowObjects == null || rowObjects.Length != Rows.Length)
                    return true;
                for (int i = 0; i < Rows.Length; i++)
                {
                    if (rowObjects[i] == null || rowObjects[i].name != "Row_" + Rows[i].Label)
                        return true;
                }
                return false;
            }
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            const float rowH = 27f, groupH = 40f, headerH = 52f, width = 300f;
            HudUiKit.ClearChildren(transform);
            foreach (Component old in new Component[] { GetComponent<VerticalLayoutGroup>(), GetComponent<ContentSizeFitter>() })
            {
                if (old != null)
                    DestroyImmediate(old);
            }

            var root = (RectTransform)transform;
            HudUiKit.Place(root, new Vector2(0f, 0f), new Vector2(44f, 36f), new Vector2(width, 200f), new Vector2(0f, 0f));
            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 20, 0, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image glow = HudUiKit.NewImage("Glow", transform, HudUiKit.Load("soft_glow"), Color.white);
            glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            glow.rectTransform.anchorMin = Vector2.zero;
            glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.offsetMin = new Vector2(-130f, -95f);
            glow.rectTransform.offsetMax = new Vector2(130f, 95f);

            RectTransform header = Block("Header", headerH);
            HudUiKit.BuildHeader(header, "STATS", width - 20f);

            groupObjects = new GameObject[GroupTitles.Length];
            rowObjects = new GameObject[Rows.Length];
            values = new TMP_Text[Rows.Length];
            int g = 0;
            for (int i = 0; i < Rows.Length; i++)
            {
                if (g < GroupStart.Length && i == GroupStart[g])
                {
                    RectTransform group = Block("Group_" + GroupTitles[g], groupH);
                    var title = HudUiKit.NewText("Title", group, GroupTitles[g], 15f, HudUiKit.Cyan, TextAlignmentOptions.MidlineLeft, 8f);
                    HudUiKit.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, -2f), new Vector2(80f, 24f));
                    Image line = HudUiKit.NewImage("Line", group, HudUiKit.Load("divider"), new Color(1f, 1f, 1f, 0.35f));
                    HudUiKit.Place(line.rectTransform, new Vector2(0f, 0.5f), new Vector2(60f, -2f), new Vector2(width - 80f, 12f));
                    groupObjects[g++] = group.gameObject;
                }

                bool key = Rows[i].Key;
                RectTransform row = Block("Row_" + Rows[i].Label, rowH);
                Image bullet = HudUiKit.NewImage("Bullet", row, null, key ? HudUiKit.Gold : new Color(0.51f, 0.88f, 1f, 0.55f));
                HudUiKit.Place(bullet.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(6f, 6f), new Vector2(0.5f, 0.5f));
                bullet.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                TMP_Text label = HudUiKit.NewText("Label", row, Rows[i].Label, 17f, key ? HudUiKit.Label : new Color(0.69f, 0.75f, 0.82f, 0.8f), TextAlignmentOptions.MidlineLeft);
                HudUiKit.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(170f, rowH), new Vector2(0f, 0.5f));

                values[i] = HudUiKit.NewText("Value", row, "-", 19f, key ? HudUiKit.Value : new Color(0.93f, 0.96f, 0.98f, 0.85f), TextAlignmentOptions.MidlineRight);
                HudUiKit.Place(values[i].rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(170f, rowH), new Vector2(1f, 0.5f));
                rowObjects[i] = row.gameObject;
            }

            RectTransform hintRow = Block("Hint", 26f);
            var hintText = HudUiKit.NewText("Text", hintRow, "Ctrl  전체 보기", 13f, new Color(0.69f, 0.75f, 0.82f, 0.55f), TextAlignmentOptions.MidlineRight);
            HudUiKit.Stretch(hintText.rectTransform);
            hint = hintRow.gameObject;

            _map = null;
            Apply(false);
#endif
        }

#if UNITY_EDITOR
        private RectTransform Block(string name, float height)
        {
            RectTransform rect = HudUiKit.NewRect(name, transform);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return rect;
        }
#endif

        private void OnEnable()
        {
            if (playerStatsChannel == null || _listening)
                return;

            _listening = true;
            playerStatsChannel.AddListener<PlayerStatsChangedEvent>(OnStats);
            playerStatsChannel.RaiseEvent(PlayerStatsEvents.PlayerStatsRequestEvent);
        }

        private void OnDisable()
        {
            if (playerStatsChannel != null && _listening)
                playerStatsChannel.RemoveListener<PlayerStatsChangedEvent>(OnStats);
            _listening = false;
        }

        private void OnStats(PlayerStatsChangedEvent evt)
        {
            _last = evt;
            _hasLast = true;
            if (values == null || rowObjects == null)
                return;

            PlayerHudSource hud = PlayerHudSource.Instance;
            int[] map = Map();
            for (int i = 0; i < values.Length && i < map.Length; i++)
            {
                int r = map[i];
                if (r < 0 || values[i] == null)
                    continue;
                float bonus = hud != null ? hud.ComboStatBonus(Rows[r].Id) : 0f;
                values[i].text = FormatValue(Rows[r].Format, ValueOf(Rows[r], evt)) + FormatBonus(Rows[r].Format, bonus);
            }
        }

        private static readonly string BonusColor = ColorUtility.ToHtmlStringRGB(HudUiKit.Cyan);

        private static string FormatBonus(Fmt format, float bonus)
        {
            if (Mathf.Abs(bonus) < 0.0005f)
                return string.Empty;

            string sign = bonus > 0f ? "+" : "-";
            float v = Mathf.Abs(bonus);
            string text = format == Fmt.Percent ? $"{v * 100f:0}%" : $"{v:0.#}";
            return $"  <size=80%><color=#{BonusColor}>{sign}{text}</color></size>";
        }

        private static string FormatValue(Fmt format, float v)
        {
            switch (format)
            {
                case Fmt.Percent: return $"{v * 100f:0}%";
                case Fmt.Multiplier: return $"x{v:0.#}";
                case Fmt.CritMultiplier: return $"x{v:0.##}";
                case Fmt.DefenseRating:
                    return $"{v:0} <size=80%><color=#80E0FF>{StatFormulas.Mitigation(v, StatFormulas.DefenseK) * 100f:0}%</color></size>";
                case Fmt.ElementRating:
                    return $"{v:0} <size=80%><color=#80E0FF>{StatFormulas.Mitigation(v, StatFormulas.ElementK) * 100f:0}%</color></size>";
                default: return $"{v:0.#}";
            }
        }
    }
}
