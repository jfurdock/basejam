using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using MLBShowdown.Cards;
using MLBShowdown.Network;

namespace MLBShowdown.UI
{
    public class LineupDisplayUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private Transform homeLineupContainer;
        [SerializeField] private Transform awayLineupContainer;
        [SerializeField] private GameObject lineupRowPrefab;

        [Header("Headers")]
        [SerializeField] private TextMeshProUGUI homeTeamHeader;
        [SerializeField] private TextMeshProUGUI awayTeamHeader;

        [Header("Pitcher Display")]
        [SerializeField] private TextMeshProUGUI homePitcherText;
        [SerializeField] private TextMeshProUGUI awayPitcherText;

        [Header("Current Batter Highlight")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color currentBatterColor = Color.yellow;
        [SerializeField] private Color onBaseColor = Color.green;

        private List<LineupRowUI> homeRows = new List<LineupRowUI>();
        private List<LineupRowUI> awayRows = new List<LineupRowUI>();
        private NetworkGameManager gameManager;

        void Start()
        {
            // Create containers if not assigned
            if (homeLineupContainer == null)
            {
                homeLineupContainer = CreateLineupContainer("HomeLineup", new Vector2(-200, 0));
            }
            if (awayLineupContainer == null)
            {
                awayLineupContainer = CreateLineupContainer("AwayLineup", new Vector2(200, 0));
            }
        }

        void Update()
        {
            if (gameManager == null)
            {
                gameManager = NetworkGameManager.Instance;
                if (gameManager != null)
                {
                    RefreshLineups();
                }
            }
            else
            {
                UpdateCurrentBatterHighlight();
            }
        }

        private Transform CreateLineupContainer(string name, Vector2 position)
        {
            GameObject containerObj = new GameObject(name);
            containerObj.transform.SetParent(transform);
            
            RectTransform rect = containerObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(180, 400);

            VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            return containerObj.transform;
        }

        public void RefreshLineups()
        {
            if (gameManager == null) return;

            // Clear existing rows
            ClearRows(homeRows, homeLineupContainer);
            ClearRows(awayRows, awayLineupContainer);

            // Create home lineup
            for (int i = 0; i < 9; i++)
            {
                int batterIndex = gameManager.HomeBatterIndices.Get(i);
                var batter = gameManager.GetBatter(batterIndex);
                if (batter != null)
                {
                    var row = CreateLineupRow(homeLineupContainer, i + 1, batter);
                    homeRows.Add(row);
                }
            }

            // Create away lineup
            for (int i = 0; i < 9; i++)
            {
                int batterIndex = gameManager.AwayBatterIndices.Get(i);
                var batter = gameManager.GetBatter(batterIndex);
                if (batter != null)
                {
                    var row = CreateLineupRow(awayLineupContainer, i + 1, batter);
                    awayRows.Add(row);
                }
            }

            // Update pitcher displays
            UpdatePitcherDisplay();

            // Update headers
            if (homeTeamHeader != null) homeTeamHeader.text = "HOME";
            if (awayTeamHeader != null) awayTeamHeader.text = "AWAY";
        }

        private void ClearRows(List<LineupRowUI> rows, Transform container)
        {
            foreach (var row in rows)
            {
                if (row != null && row.gameObject != null)
                {
                    Destroy(row.gameObject);
                }
            }
            rows.Clear();

            // Also clear any orphaned children
            if (container != null)
            {
                foreach (Transform child in container)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private LineupRowUI CreateLineupRow(Transform parent, int orderNum, BatterCardData batter)
        {
            GameObject rowObj;
            if (lineupRowPrefab != null)
            {
                rowObj = Instantiate(lineupRowPrefab, parent);
            }
            else
            {
                rowObj = CreateDefaultLineupRow(parent);
            }

            LineupRowUI row = rowObj.GetComponent<LineupRowUI>();
            if (row == null)
            {
                row = rowObj.AddComponent<LineupRowUI>();
            }

            row.Setup(orderNum, batter);
            return row;
        }

        private GameObject CreateDefaultLineupRow(Transform parent)
        {
            GameObject rowObj = new GameObject("LineupRow");
            rowObj.transform.SetParent(parent);

            RectTransform rect = rowObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180, 25);

            HorizontalLayoutGroup layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(5, 5, 2, 2);

            // Background
            Image bg = rowObj.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Order number
            CreateTextElement(rowObj.transform, "Order", "1", 20);
            // Name
            CreateTextElement(rowObj.transform, "Name", "Player Name", 100);
            // Position
            CreateTextElement(rowObj.transform, "Position", "POS", 30);

            return rowObj;
        }

        private void CreateTextElement(Transform parent, string name, string defaultText, float width)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 20);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = defaultText;
            text.fontSize = 12;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;

            LayoutElement layoutElement = textObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
        }

        private void UpdatePitcherDisplay()
        {
            if (gameManager == null) return;

            var homePitcher = gameManager.GetPitcher(gameManager.HomePitcherIndex);
            var awayPitcher = gameManager.GetPitcher(gameManager.AwayPitcherIndex);

            if (homePitcherText != null && homePitcher != null)
            {
                homePitcherText.text = $"P: {homePitcher.PlayerName} (CTRL: {homePitcher.Control})";
            }

            if (awayPitcherText != null && awayPitcher != null)
            {
                awayPitcherText.text = $"P: {awayPitcher.PlayerName} (CTRL: {awayPitcher.Control})";
            }
        }

        private void UpdateCurrentBatterHighlight()
        {
            if (gameManager == null) return;

            int homeBatterUp = gameManager.HomeBatterUp;
            int awayBatterUp = gameManager.AwayBatterUp;
            bool isTopOfInning = gameManager.IsTopOfInning;

            // Update home lineup highlights
            for (int i = 0; i < homeRows.Count; i++)
            {
                if (homeRows[i] != null)
                {
                    bool isCurrent = !isTopOfInning && i == homeBatterUp;
                    homeRows[i].SetHighlight(isCurrent ? currentBatterColor : normalColor);
                }
            }

            // Update away lineup highlights
            for (int i = 0; i < awayRows.Count; i++)
            {
                if (awayRows[i] != null)
                {
                    bool isCurrent = isTopOfInning && i == awayBatterUp;
                    awayRows[i].SetHighlight(isCurrent ? currentBatterColor : normalColor);
                }
            }
        }
    }

    public class LineupRowUI : MonoBehaviour
    {
        private TextMeshProUGUI orderText;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI positionText;
        private Image background;
        private BatterCardData batterData;

        public void Setup(int order, BatterCardData batter)
        {
            batterData = batter;

            // Find or create text components
            orderText = transform.Find("Order")?.GetComponent<TextMeshProUGUI>();
            nameText = transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            positionText = transform.Find("Position")?.GetComponent<TextMeshProUGUI>();
            background = GetComponent<Image>();

            if (orderText != null) orderText.text = order.ToString();
            if (nameText != null) nameText.text = batter.PlayerName;
            if (positionText != null) positionText.text = batter.Position;
        }

        public void SetHighlight(Color color)
        {
            if (nameText != null) nameText.color = color;
            if (orderText != null) orderText.color = color;
            if (positionText != null) positionText.color = color;
        }

        public void UpdateStats()
        {
            // Could show AB/H/HR inline if space permits
        }
    }
}
