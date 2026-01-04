using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MLBShowdown.Core;

namespace MLBShowdown.UI
{
    public class BoxScoreUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private RectTransform boxScoreContainer;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI awayScoreText;
        [SerializeField] private TextMeshProUGUI homeScoreText;

        [Header("Team Stats")]
        [SerializeField] private TextMeshProUGUI awayStatsText;
        [SerializeField] private TextMeshProUGUI homeStatsText;

        [Header("Style")]
        [SerializeField] private Color headerColor = new Color(0.8f, 0.8f, 0.8f);
        [SerializeField] private Color scoreColor = Color.white;
        [SerializeField] private TMP_FontAsset monoFont;

        private GameStatistics gameStats;

        void Start()
        {
            if (boxScoreContainer == null)
            {
                CreateBoxScoreLayout();
            }
        }

        void Update()
        {
            if (gameStats == null)
            {
                gameStats = FindObjectOfType<GameStatistics>();
            }

            if (gameStats != null)
            {
                UpdateDisplay();
            }
        }

        private void CreateBoxScoreLayout()
        {
            GameObject containerObj = new GameObject("BoxScoreContainer");
            containerObj.transform.SetParent(transform);

            boxScoreContainer = containerObj.AddComponent<RectTransform>();
            boxScoreContainer.anchorMin = new Vector2(0.5f, 0.5f);
            boxScoreContainer.anchorMax = new Vector2(0.5f, 0.5f);
            boxScoreContainer.sizeDelta = new Vector2(500, 200);
            boxScoreContainer.anchoredPosition = Vector2.zero;

            var bg = containerObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.padding = new RectOffset(15, 15, 10, 10);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;

            // Header row
            headerText = CreateTextRow(containerObj.transform, "Header", "     1  2  3  4  5  6  7  8  9   R  H  E");
            headerText.color = headerColor;

            // Away score row
            awayScoreText = CreateTextRow(containerObj.transform, "AwayScore", "AWAY 0  0  0  0  0  0  0  0  0   0  0  0");

            // Home score row
            homeScoreText = CreateTextRow(containerObj.transform, "HomeScore", "HOME 0  0  0  0  0  0  0  0  0   0  0  0");

            // Separator
            CreateSeparator(containerObj.transform);

            // Stats section
            awayStatsText = CreateTextRow(containerObj.transform, "AwayStats", "AWAY: AVG .000 | OBP .000 | SLG .000");
            awayStatsText.fontSize = 14;

            homeStatsText = CreateTextRow(containerObj.transform, "HomeStats", "HOME: AVG .000 | OBP .000 | SLG .000");
            homeStatsText.fontSize = 14;
        }

        private TextMeshProUGUI CreateTextRow(Transform parent, string name, string defaultText)
        {
            GameObject rowObj = new GameObject(name);
            rowObj.transform.SetParent(parent);

            RectTransform rect = rowObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 25);

            var text = rowObj.AddComponent<TextMeshProUGUI>();
            text.text = defaultText;
            text.fontSize = 16;
            text.color = scoreColor;
            text.alignment = TextAlignmentOptions.Left;
            
            if (monoFont != null)
            {
                text.font = monoFont;
            }

            LayoutElement layoutElement = rowObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 25;

            return text;
        }

        private void CreateSeparator(Transform parent)
        {
            GameObject sepObj = new GameObject("Separator");
            sepObj.transform.SetParent(parent);

            RectTransform rect = sepObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 2);

            var img = sepObj.AddComponent<Image>();
            img.color = new Color(0.4f, 0.4f, 0.4f);

            LayoutElement layoutElement = sepObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 2;
        }

        private void UpdateDisplay()
        {
            if (gameStats == null) return;

            if (headerText != null)
            {
                headerText.text = gameStats.GetBoxScoreHeader();
            }

            if (awayScoreText != null)
            {
                awayScoreText.text = gameStats.GetAwayBoxScore();
            }

            if (homeScoreText != null)
            {
                homeScoreText.text = gameStats.GetHomeBoxScore();
            }

            if (awayStatsText != null)
            {
                var stats = gameStats.AwayTeamStats;
                awayStatsText.text = $"AWAY: AVG {stats.GetBattingAverage():F3} | OBP {stats.GetOnBasePercentage():F3} | SLG {stats.GetSluggingPercentage():F3}";
            }

            if (homeStatsText != null)
            {
                var stats = gameStats.HomeTeamStats;
                homeStatsText.text = $"HOME: AVG {stats.GetBattingAverage():F3} | OBP {stats.GetOnBasePercentage():F3} | SLG {stats.GetSluggingPercentage():F3}";
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
        }
    }
}
