using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MLBShowdown.Cards;
using MLBShowdown.Core;

namespace MLBShowdown.UI
{
    public class CardDisplayUI : MonoBehaviour
    {
        [Header("Card Frame")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image cardBorder;
        [SerializeField] private Color batterCardColor = new Color(0.2f, 0.4f, 0.8f);
        [SerializeField] private Color pitcherCardColor = new Color(0.8f, 0.2f, 0.2f);

        [Header("Player Info")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI positionText;
        [SerializeField] private Image playerImage;

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI stat1Label;
        [SerializeField] private TextMeshProUGUI stat1Value;
        [SerializeField] private TextMeshProUGUI stat2Label;
        [SerializeField] private TextMeshProUGUI stat2Value;
        [SerializeField] private TextMeshProUGUI stat3Label;
        [SerializeField] private TextMeshProUGUI stat3Value;

        [Header("Outcome Chart")]
        [SerializeField] private Transform outcomeChartContainer;
        [SerializeField] private GameObject outcomeRowPrefab;

        [Header("Game Stats")]
        [SerializeField] private TextMeshProUGUI gameStatsText;

        public void DisplayBatterCard(BatterCardData batter)
        {
            if (batter == null) return;

            // Set card color
            if (cardBackground != null)
                cardBackground.color = batterCardColor;

            // Player info
            if (playerNameText != null)
                playerNameText.text = batter.PlayerName;
            
            if (positionText != null)
                positionText.text = batter.Position;

            // Stats
            SetStat(stat1Label, stat1Value, "ON BASE", batter.OnBase.ToString());
            SetStat(stat2Label, stat2Value, "SPEED", batter.Speed.ToString());
            SetStat(stat3Label, stat3Value, "FIELD+", batter.PositionPlus.ToString());

            // Outcome chart
            DisplayOutcomeChart(batter.OutcomeCard);

            // Game stats
            if (gameStatsText != null)
            {
                gameStatsText.text = $"AB: {batter.AtBats} | H: {batter.Hits} | HR: {batter.HomeRuns}\n" +
                                    $"RBI: {batter.RBIs} | R: {batter.Runs} | AVG: {batter.GetBattingAverage():F3}";
            }
        }

        public void DisplayPitcherCard(PitcherCardData pitcher)
        {
            if (pitcher == null) return;

            // Set card color
            if (cardBackground != null)
                cardBackground.color = pitcherCardColor;

            // Player info
            if (playerNameText != null)
                playerNameText.text = pitcher.PlayerName;
            
            if (positionText != null)
                positionText.text = "P";

            // Stats
            SetStat(stat1Label, stat1Value, "CONTROL", pitcher.Control.ToString());
            SetStat(stat2Label, stat2Value, "INNINGS", pitcher.Innings.ToString());
            SetStat(stat3Label, stat3Value, "", "");

            // Outcome chart
            DisplayOutcomeChart(pitcher.OutcomeCard);

            // Game stats
            if (gameStatsText != null)
            {
                gameStatsText.text = $"IP: {pitcher.InningsPitched:F1} | K: {pitcher.Strikeouts} | BB: {pitcher.Walks}\n" +
                                    $"H: {pitcher.Hits} | ER: {pitcher.EarnedRuns} | ERA: {pitcher.GetERA():F2}";
            }
        }

        private void SetStat(TextMeshProUGUI label, TextMeshProUGUI value, string labelText, string valueText)
        {
            if (label != null) label.text = labelText;
            if (value != null) value.text = valueText;
        }

        private void DisplayOutcomeChart(OutcomeCard outcomeCard)
        {
            if (outcomeChartContainer == null || outcomeCard == null) return;

            // Clear existing rows
            foreach (Transform child in outcomeChartContainer)
            {
                Destroy(child.gameObject);
            }

            // Create outcome rows
            CreateOutcomeRow("Strikeout", outcomeCard.Strikeout, Color.red);
            CreateOutcomeRow("Groundout", outcomeCard.Groundout, new Color(0.6f, 0.4f, 0.2f));
            CreateOutcomeRow("Flyout", outcomeCard.Flyout, new Color(0.5f, 0.5f, 0.5f));
            CreateOutcomeRow("Walk", outcomeCard.Walk, Color.cyan);
            CreateOutcomeRow("Single", outcomeCard.Single, Color.green);
            CreateOutcomeRow("Double", outcomeCard.Double, Color.yellow);
            CreateOutcomeRow("Triple", outcomeCard.Triple, new Color(1f, 0.5f, 0f));
            CreateOutcomeRow("Home Run", outcomeCard.HomeRun, new Color(1f, 0f, 1f));
        }

        private void CreateOutcomeRow(string outcomeName, OutcomeRange range, Color color)
        {
            if (range == null) return;

            GameObject row;
            if (outcomeRowPrefab != null)
            {
                row = Instantiate(outcomeRowPrefab, outcomeChartContainer);
            }
            else
            {
                row = new GameObject(outcomeName);
                row.transform.SetParent(outcomeChartContainer);
                
                var layoutGroup = row.AddComponent<HorizontalLayoutGroup>();
                layoutGroup.spacing = 10;
                layoutGroup.childAlignment = TextAnchor.MiddleLeft;

                // Outcome name
                var nameObj = new GameObject("Name");
                nameObj.transform.SetParent(row.transform);
                var nameText = nameObj.AddComponent<TextMeshProUGUI>();
                nameText.text = outcomeName;
                nameText.fontSize = 14;
                nameText.color = color;

                // Range
                var rangeObj = new GameObject("Range");
                rangeObj.transform.SetParent(row.transform);
                var rangeText = rangeObj.AddComponent<TextMeshProUGUI>();
                rangeText.text = range.MinRoll == range.MaxRoll ? 
                    range.MinRoll.ToString() : 
                    $"{range.MinRoll}-{range.MaxRoll}";
                rangeText.fontSize = 14;
                rangeText.color = Color.white;
            }
        }

        public void Clear()
        {
            if (playerNameText != null) playerNameText.text = "";
            if (positionText != null) positionText.text = "";
            if (gameStatsText != null) gameStatsText.text = "";
            
            if (outcomeChartContainer != null)
            {
                foreach (Transform child in outcomeChartContainer)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }
}
