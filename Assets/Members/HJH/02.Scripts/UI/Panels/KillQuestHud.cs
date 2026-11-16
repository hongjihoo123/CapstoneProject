using Assets.Members.HJH._02.Scripts.Quest;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 우상단 목표 패널: "숲의 적 처치  잡은 수 / 전체 적의 수".
    // 값은 RoomClearTracker(적이 죽을 때 AbstractEnemy.Dead 가 알림) 에서 받고, 다 잡으면 "목표 완료!" 로 바뀌며
    // RoomPortalGate 가 숨겨 둔 포탈을 열어 줌. 방에 적이 하나도 없으면(상점 등) 패널을 숨김.
    public class KillQuestHud : MonoBehaviour
    {
        [SerializeField] private string title = "숲의 적 처치";
        [SerializeField] private string description = "정글의 몬스터를 처치하세요";
        [SerializeField] private string clearedTitle = "목표 완료!";
        [SerializeField] private string clearedDescription = "포탈이 열렸습니다";
        [SerializeField] private Vector2 size = new(360f, 84f);
        [SerializeField, Tooltip("적이 있는 방에서는 클리어 전까지 포탈을 숨김")]
        private bool lockPortalUntilCleared = true;

        private RoomClearTracker _tracker;
        private CanvasGroup _group;
        private TMP_Text _count;
        private TMP_Text _title;
        private TMP_Text _tag;
        private TMP_Text _desc;
        private Image _bar;
        private bool _shownCleared;

        private static readonly Color BarColor = new(0.35f, 0.8f, 0.96f, 1f);

        private void Awake()
        {
            var rect = (RectTransform)transform;
            HudUiKit.Place(rect, new Vector2(1f, 1f), new Vector2(-40f, -250f), size);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            Image panel = HudUiKit.NewImage("Panel", transform, HudUiKit.Load("panel_9slice"), Color.white, Image.Type.Sliced);
            HudUiKit.Stretch(panel.rectTransform);

            Image icon = HudUiKit.NewImage("Icon", transform, HudUiKit.Load("quest_icon"), Color.white);
            HudUiKit.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -12f), new Vector2(26f, 26f));

            _tag = HudUiKit.NewText("Tag", transform, "목표", 15f, HudUiKit.Cyan);
            HudUiKit.Place(_tag.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -12f), new Vector2(50f, 26f));
            _tag.alignment = TextAlignmentOptions.MidlineLeft;

            _title = HudUiKit.NewText("Title", transform, title, 19f, HudUiKit.Title);
            HudUiKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(96f, -12f), new Vector2(190f, 26f));
            _title.alignment = TextAlignmentOptions.MidlineLeft;

            _count = HudUiKit.NewText("Count", transform, string.Empty, 19f, HudUiKit.Cyan, TextAlignmentOptions.MidlineRight);
            HudUiKit.Place(_count.rectTransform, new Vector2(1f, 1f), new Vector2(-18f, -12f), new Vector2(90f, 26f));

            _desc = HudUiKit.NewText("Description", transform, description, 14f, HudUiKit.Label);
            HudUiKit.Place(_desc.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -42f), new Vector2(size.x - 32f, 22f));
            _desc.alignment = TextAlignmentOptions.MidlineLeft;

            Image barBack = HudUiKit.NewImage("BarBack", transform, null, new Color(0.03f, 0.05f, 0.09f, 0.8f));
            HudUiKit.Place(barBack.rectTransform, new Vector2(0f, 0f), new Vector2(16f, 10f), new Vector2(size.x - 32f, 5f));
            // 스프라이트 없는 Image 는 Filled 가 안 먹어서 가로 앵커로 길이를 조절
            _bar = HudUiKit.NewImage("Bar", barBack.transform, null, BarColor);
            HudUiKit.Stretch(_bar.rectTransform);

            _tracker = GetComponent<RoomClearTracker>();
            if (_tracker == null)
                _tracker = gameObject.AddComponent<RoomClearTracker>();

            if (lockPortalUntilCleared)
            {
                var gate = gameObject.AddComponent<RoomPortalGate>();
                gate.Bind(_tracker);
            }
        }

        private void OnEnable()
        {
            if (_tracker == null)
                return;
            _tracker.Changed += Refresh;
            _tracker.Cleared += OnCleared;
            Refresh();
        }

        private void OnDisable()
        {
            if (_tracker == null)
                return;
            _tracker.Changed -= Refresh;
            _tracker.Cleared -= OnCleared;
        }

        private void Start() => Refresh();

        private void Refresh()
        {
            if (_tracker == null || _count == null)
                return;

            _group.alpha = _tracker.Total > 0 ? 1f : 0f;   // 적 없는 방이면 숨김

            int total = Mathf.Max(0, _tracker.Total);
            int kills = Mathf.Min(_tracker.Kills, total);
            _count.text = $"{kills} / {total}";
            _bar.rectTransform.anchorMax = new Vector2(total > 0 ? (float)kills / total : 0f, 1f);

            if (_tracker.IsCleared)
                ShowCleared();
        }

        private void OnCleared()
        {
            Refresh();
            // 완료 순간 패널이 톡 튐
            ((RectTransform)transform).DOKill(true);
            ((RectTransform)transform).DOPunchScale(Vector3.one * 0.12f, 0.35f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
        }

        private void ShowCleared()
        {
            if (_shownCleared)
                return;

            _shownCleared = true;
            _tag.text = "완료";
            _tag.color = HudUiKit.Gold;
            _title.text = clearedTitle;
            _title.color = HudUiKit.Gold;
            _count.color = HudUiKit.Gold;
            _desc.text = clearedDescription;
            _bar.color = HudUiKit.Gold;
        }
    }
}
