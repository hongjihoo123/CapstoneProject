using Members.JJH._02_Scripts.Systems.EventChannelSystem;
using Members.JJH._02_Scripts.Systems.EventChannelSystem.Events;
using Members.KYR._01.Scripts.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 좌하단 간단 스탯 (박스 없이 부드러운 어둠 위에 글자). 값은 PlayerStats 이벤트 채널에서 받음.
    // 전체 스탯 창(PlayerStatsUI)은 그대로 두고, 자주 보는 몇 개만 보여줌.
    public class StatsMiniHud : MonoBehaviour
    {
        private enum Fmt { Number, Percent, Multiplier }

        private readonly struct Row
        {
            public readonly PlayerStatId Id;
            public readonly string Label;
            public readonly Fmt Format;
            public Row(PlayerStatId id, string label, Fmt format) { Id = id; Label = label; Format = format; }
        }

        private static readonly Row[] Rows =
        {
            new(PlayerStatId.Damage, "공격력", Fmt.Multiplier),
            new(PlayerStatId.AttackSpeed, "공격 속도", Fmt.Multiplier),
            new(PlayerStatId.MaxHp, "최대 체력", Fmt.Number),
            new(PlayerStatId.DamageReduction, "피해 감소", Fmt.Percent),
            new(PlayerStatId.MoveSpeed, "이동 속도", Fmt.Number),
        };

        [SerializeField] private EventChannelSO playerStatsChannel;
        [SerializeField] private Vector2 size = new(300f, 230f);

        private readonly TMP_Text[] _values = new TMP_Text[Rows.Length];
        private bool _listening;

        private void Awake()
        {
            var rect = (RectTransform)transform;
            HudUiKit.Place(rect, new Vector2(0f, 0f), new Vector2(44f, 36f), size);

            Image glow = HudUiKit.NewImage("Glow", transform, HudUiKit.Load("soft_glow"), Color.white);
            HudUiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(260f, 190f));

            RectTransform content = HudUiKit.NewRect("Content", transform);
            HudUiKit.Stretch(content);
            HudUiKit.BuildHeader(content, "STATS", size.x - 20f);

            for (int i = 0; i < Rows.Length; i++)
            {
                float y = -62f - i * 32f;
                Image bullet = HudUiKit.NewImage("Bullet" + i, content, null, HudUiKit.Gold);
                HudUiKit.Place(bullet.rectTransform, new Vector2(0f, 1f), new Vector2(8f, y - 12f), new Vector2(6f, 6f));
                bullet.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                TMP_Text label = HudUiKit.NewText("Label" + i, content, Rows[i].Label, 18f, HudUiKit.Label, TextAlignmentOptions.MidlineLeft);
                HudUiKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(26f, y), new Vector2(150f, 28f));

                _values[i] = HudUiKit.NewText("Value" + i, content, "-", 20f, HudUiKit.Value, TextAlignmentOptions.MidlineRight);
                HudUiKit.Place(_values[i].rectTransform, new Vector2(0f, 1f), new Vector2(size.x - 20f - 110f, y), new Vector2(110f, 28f));
            }
        }

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
            for (int i = 0; i < Rows.Length; i++)
            {
                float v = evt.Get(Rows[i].Id);
                _values[i].text = Rows[i].Format switch
                {
                    Fmt.Percent => $"{v * 100f:0.#}%",
                    Fmt.Multiplier => $"x{v:0.##}",
                    _ => $"{v:0.##}"
                };
            }
        }
    }
}
