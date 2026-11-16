using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Members.HJH._02.Scripts.UI
{
    // 상단 가운데: 경과 시간 | 스테이지 이름. 이름을 비워 두면 씬 이름을 씀.
    public class StageTimerHud : MonoBehaviour
    {
        [SerializeField] private string stageName;

        private TMP_Text _time;

        private void Awake()
        {
            var rect = (RectTransform)transform;
            HudUiKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(520f, 48f));

            _time = HudUiKit.NewText("Time", transform, "00:00", 32f, HudUiKit.Title, TextAlignmentOptions.MidlineRight, 2f);
            HudUiKit.Place(_time.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-18f, 0f), new Vector2(200f, 48f), new Vector2(1f, 0.5f));

            var line = HudUiKit.NewImage("Divider", transform, null, new Color(0.75f, 0.85f, 0.92f, 0.5f));
            HudUiKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1.5f, 30f));

            string label = string.IsNullOrEmpty(stageName) ? SceneManager.GetActiveScene().name : stageName;
            TMP_Text stage = HudUiKit.NewText("Stage", transform, label, 22f, HudUiKit.Title, TextAlignmentOptions.MidlineLeft);
            HudUiKit.Place(stage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(18f, 0f), new Vector2(300f, 48f), new Vector2(0f, 0.5f));
        }

        private void Update()
        {
            int total = Mathf.FloorToInt(Time.timeSinceLevelLoad);
            _time.text = $"{total / 60:00}:{total % 60:00}";
        }
    }
}
