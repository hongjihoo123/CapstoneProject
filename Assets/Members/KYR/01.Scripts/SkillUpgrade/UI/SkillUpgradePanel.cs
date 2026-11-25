using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Members.KYR._01.Scripts.SkillUpgrade.UI
{
    // 스킬 강화 화면. 프리팹(SkillUpgradePanel)의 루트 Canvas에 붙는다.
    // 무엇을 보여 줄지는 SkillUpgradeShrine이 정하고, 이 컴포넌트는 그리기만 한다.
    public class SkillUpgradePanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text remainingLabel;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private SkillUpgradeRow rowPrefab;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private Button closeButton;

        private readonly List<SkillUpgradeRow> _rows = new List<SkillUpgradeRow>();

        public event Action CloseClicked;

        public bool IsShown => root != null && root.activeSelf;

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(() => CloseClicked?.Invoke());
            Hide();
        }

        public void Show()
        {
            if (root != null)
                root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }

        public void SetHeader(string title, int gold, int remaining)
        {
            if (titleLabel != null)
                titleLabel.text = title;
            if (goldLabel != null)
                goldLabel.text = gold.ToString();
            if (remainingLabel != null)
            {
                remainingLabel.gameObject.SetActive(remaining >= 0);
                remainingLabel.text = $"남은 강화 {remaining}회";
            }
        }

        // 줄은 재사용한다: 모자라면 만들고, 남으면 숨긴다.
        public void SetRows(IReadOnlyList<SkillUpgradeRowData> rows, Action<int> onClick)
        {
            int count = rows != null ? rows.Count : 0;
            while (_rows.Count < count && rowPrefab != null)
                _rows.Add(Instantiate(rowPrefab, rowContainer));

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < count;
                _rows[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                int index = i;
                _rows[i].Bind(rows[i], () => onClick?.Invoke(index));
            }

            if (emptyLabel != null)
                emptyLabel.gameObject.SetActive(count == 0);
        }
    }
}
