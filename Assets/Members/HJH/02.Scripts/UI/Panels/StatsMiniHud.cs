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
    // 좌하단 스탯 (박스 없이 부드러운 어둠 위에 글자). 값은 PlayerStats 이벤트 채널에서 받음.
    // 평소엔 핵심 6개(★)만 보이고, Ctrl 을 누르고 있으면 아래는 그대로 두고 위로 펼쳐져 전체 스탯이 보인다.
    // 전체 스탯 창(PlayerStatsUI)은 건드리지 않음.
    public class StatsMiniHud : MonoBehaviour, IUiBake
    {
        private enum Fmt { Number, Percent, Multiplier, DefenseRating, ElementRating, CritMultiplier }

        private readonly struct Row
        {
            public readonly PlayerStatId Id;
            public readonly string Label;
            public readonly Fmt Format;
            public readonly bool Key;   // true = 평소에도 보이는 핵심 스탯
            public Row(PlayerStatId id, string label, Fmt format, bool key = false) { Id = id; Label = label; Format = format; Key = key; }
        }

        // 그룹 제목은 null Id 대신 별도 배열로: 각 그룹의 시작 인덱스
        private static readonly string[] GroupTitles = { "공격", "방어", "이동" };
        private static readonly int[] GroupStart = { 0, 14, 21 };

        private static readonly Row[] Rows =
        {
            // 공격
            new(PlayerStatId.Damage, "공격력", Fmt.Multiplier, true),
            new(PlayerStatId.MagicDamage, "마법 공격력", Fmt.Multiplier, true),
            new(PlayerStatId.AttackSpeed, "공격 속도", Fmt.Multiplier, true),
            // 치명: 스탯 트리 값 + 스킬 트리(평타 칸) 값. 전투 쪽(CritSystem)도 같은 두 값을 더해서 씀
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
            // 방어
            new(PlayerStatId.MaxHp, "최대 체력", Fmt.Number, true),
            new(PlayerStatId.DamageReduction, "피해 감소", Fmt.Percent),
            new(PlayerStatId.IncomingDamageTaken, "받는 피해 배율", Fmt.Multiplier),
            new(PlayerStatId.HealReceived, "받는 회복 배율", Fmt.Multiplier),
            new(PlayerStatId.Tenacity, "강인함", Fmt.Number),
            new(PlayerStatId.ArmorDefense, "물리 방어력", Fmt.DefenseRating, true),
            new(PlayerStatId.MagicDefense, "마법 방어력", Fmt.ElementRating, true),
            // 이동
            new(PlayerStatId.MoveSpeed, "이동 속도", Fmt.Number),
            new(PlayerStatId.DashSpeed, "대시 속도", Fmt.Number),
            new(PlayerStatId.DashDuration, "대시 지속시간", Fmt.Number),
        };

        [SerializeField] private EventChannelSO playerStatsChannel;
        [SerializeField] private Key expandKey = Key.LeftCtrl;

        // 하이어라키에 구워 둔 부품. 줄 쌓기는 VerticalLayoutGroup + ContentSizeFitter 가 함 (아래 모서리 고정, 위로 늘어남)
        // → 줄 높이·간격·글자 크기는 인스펙터에서 바로 고치면 됨. 코드는 펼칠 때 켜고 끄기만 함
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
            // 캡처 도구는 키를 누를 수 없어서, 에디터 설정으로 펼침을 강제할 수 있게 함
            held |= UnityEditor.EditorPrefs.GetBool("HJH.StatsHud.ForceExpand", false);
#endif
            if (held != _expanded)
                Apply(held);

            // 치명 값은 스킬 트리 쪽에서 오니까 이벤트가 없어도 가끔 다시 읽음
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

        // 구워 둔 줄(rowObjects) → Rows 번호. 줄 이름("Row_공격력")으로 찾아서, 옛날에 구운 프리팹도 줄이 밀리지 않음.
        // 새로 추가한 줄(치명타 등)은 다시 굽기 전까지 오브젝트가 없어서 안 보임
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

        // 평소엔 핵심(★) 줄만, 펼치면 전부 + 그룹 제목
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

        // 구워 둔 줄이 코드의 Rows 와 다르면 true (줄을 추가/삭제한 뒤). 에디터 자동 굽기가 이걸로 판단함
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

            // 뒤 어둠 (레이아웃 무시, 판보다 크게)
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
        // 레이아웃 그룹이 높이를 정하는 한 덩어리
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
            // 콤보 효과가 주는 몫은 기본 값 뒤에 "+12" 처럼 청록색으로 따로 붙임 (효과가 켜지고 꺼질 때 이 이벤트가 바로 옴)
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
