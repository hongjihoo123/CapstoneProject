using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 상단 가운데: 경과 시간 | 스테이지 이름. 이름을 비워 두면 씬 이름을 씀.
    // 모양은 하이어라키에 저장된 자식 그대로 (인스펙터에서 수정)
    public class StageTimerHud : MonoBehaviour, IUiBake
    {
        [SerializeField] private string stageName;
        [SerializeField] private TMP_Text time;
        [SerializeField] private TMP_Text stage;

        private void Awake()
        {
            if (stage != null)
                stage.text = string.IsNullOrEmpty(stageName) ? SceneManager.GetActiveScene().name : stageName;
        }

        private void Update()
        {
            if (time == null)
                return;
            int total = Mathf.FloorToInt(Time.timeSinceLevelLoad);
            time.text = $"{total / 60:00}:{total % 60:00}";
        }

        [ContextMenu("레이아웃 굽기 (자식 다시 만들기)")]
        public void BakeLayout()
        {
#if UNITY_EDITOR
            HudUiKit.ClearChildren(transform);
            HudUiKit.Place((RectTransform)transform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(520f, 48f));

            time = HudUiKit.NewText("Time", transform, "00:00", 32f, HudUiKit.Title, TextAlignmentOptions.MidlineRight, 2f);
            HudUiKit.Place(time.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-18f, 0f), new Vector2(200f, 48f), new Vector2(1f, 0.5f));

            var line = HudUiKit.NewImage("Divider", transform, null, new Color(0.75f, 0.85f, 0.92f, 0.5f));
            HudUiKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1.5f, 30f));

            stage = HudUiKit.NewText("Stage", transform, "Stage", 22f, HudUiKit.Title, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(stage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(18f, 0f), new Vector2(300f, 48f), new Vector2(0f, 0.5f));
#endif
        }
    }
}
