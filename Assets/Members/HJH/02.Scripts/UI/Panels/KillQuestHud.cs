using Assets.Members.HJH._02.Scripts.Quest;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 우상단 목표 패널: "숲의 적 처치  잡은 수 / 전체 적의 수".
    // 값은 RoomClearTracker(적이 죽을 때 AbstractEnemy.Dead 가 알림) 에서 받고, 다 잡으면 "목표 완료!" 로 바뀌며
    // 같은 오브젝트의 RoomPortalGate 가 숨겨 둔 포탈을 열어 줌. 방에 적이 하나도 없으면(상점 등) 패널을 숨김.
    // 모양·부품(CanvasGroup, RoomClearTracker, RoomPortalGate)은 하이어라키에 저장된 그대로
    public class KillQuestHud : MonoBehaviour, IUiBake
    {
        [SerializeField] private string clearedTitle = "목표 완료!";
        [SerializeField] private string clearedDescription = "포탈이 열렸습니다";
        [SerializeField, Tooltip("적이 있는 방에서는 클리어 전까지 포탈을 숨김 (같은 오브젝트의 RoomPortalGate)")]
        private bool lockPortalUntilCleared = true;

        [SerializeField] private RoomClearTracker tracker;
        [SerializeField] private RoomPortalGate portalGate;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text count;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text tagText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private Image bar;

        private bool _shownCleared;

        private void Awake()
        {
            if (lockPortalUntilCleared && portalGate != null && tracker != null)
                portalGate.Bind(tracker);
        }

        private void OnEnable()
        {
            if (tracker == null)
                return;
            tracker.Changed += Refresh;
            tracker.Cleared += OnCleared;
            Refresh();
        }

        private void OnDisable()
        {
            if (tracker == null)
                return;
            tracker.Changed -= Refresh;
            tracker.Cleared -= OnCleared;
        }

        private void Start() => Refresh();

        private void Refresh()
        {
            if (tracker == null || count == null)
                return;

            group.alpha = tracker.Total > 0 ? 1f : 0f;   // 적 없는 방이면 숨김

            int total = Mathf.Max(0, tracker.Total);
            int kills = Mathf.Min(tracker.Kills, total);
            count.text = $"{kills} / {total}";
            // 스프라이트 없는 Image 는 Filled 가 안 먹어서 가로 앵커로 길이를 조절
            bar.rectTransform.anchorMax = new Vector2(total > 0 ? (float)kills / total : 0f, 1f);

            if (tracker.IsCleared)
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
            tagText.text = "완료";
            tagText.color = HudUiKit.Gold;
            titleText.text = clearedTitle;
            titleText.color = HudUiKit.Gold;
            count.color = HudUiKit.Gold;
            descText.text = clearedDescription;
            bar.color = HudUiKit.Gold;
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            var size = new Vector2(360f, 84f);
            HudUiKit.Place((RectTransform)transform, new Vector2(1f, 1f), new Vector2(-40f, -250f), size);
            group = GetComponent<CanvasGroup>() ? GetComponent<CanvasGroup>() : gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            tracker = GetComponent<RoomClearTracker>() ? GetComponent<RoomClearTracker>() : gameObject.AddComponent<RoomClearTracker>();
            portalGate = GetComponent<RoomPortalGate>() ? GetComponent<RoomPortalGate>() : gameObject.AddComponent<RoomPortalGate>();

            Image panel = HudUiKit.NewImage("Panel", transform, HudUiKit.Load("panel_9slice"), Color.white, Image.Type.Sliced);
            HudUiKit.Stretch(panel.rectTransform);

            Image icon = HudUiKit.NewImage("Icon", transform, HudUiKit.Load("quest_icon"), Color.white);
            HudUiKit.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -12f), new Vector2(26f, 26f));

            tagText = HudUiKit.NewText("Tag", transform, "목표", 15f, HudUiKit.Cyan, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(tagText.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -12f), new Vector2(50f, 26f));

            titleText = HudUiKit.NewText("Title", transform, "숲의 적 처치", 19f, HudUiKit.Title, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(96f, -12f), new Vector2(190f, 26f));

            count = HudUiKit.NewText("Count", transform, "0 / 0", 19f, HudUiKit.Cyan, TextAlignmentOptions.MidlineRight);
            HudUiKit.Place(count.rectTransform, new Vector2(1f, 1f), new Vector2(-18f, -12f), new Vector2(90f, 26f));

            descText = HudUiKit.NewText("Description", transform, "정글의 몬스터를 처치하세요", 14f, HudUiKit.Label, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(descText.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -42f), new Vector2(size.x - 32f, 22f));

            Image barBack = HudUiKit.NewImage("BarBack", transform, null, new Color(0.03f, 0.05f, 0.09f, 0.8f));
            HudUiKit.Place(barBack.rectTransform, new Vector2(0f, 0f), new Vector2(16f, 10f), new Vector2(size.x - 32f, 5f));
            bar = HudUiKit.NewImage("Bar", barBack.transform, null, new Color(0.35f, 0.8f, 0.96f, 1f));
            HudUiKit.Stretch(bar.rectTransform);
#endif
        }
    }
}
