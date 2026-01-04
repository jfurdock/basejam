using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MLBShowdown.Cards;
using MLBShowdown.Core;

namespace MLBShowdown.UI
{
    public class OutcomeCardUI : MonoBehaviour
    {
        [Header("Card Layout")]
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private Image cardBackground;
        [SerializeField] private TextMeshProUGUI cardTitle;

        [Header("Outcome Rows")]
        [SerializeField] private Transform outcomeRowsContainer;
        [SerializeField] private float rowHeight = 30f;
        [SerializeField] private float rowSpacing = 5f;

        [Header("Colors")]
        [SerializeField] private Color strikeoutColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color groundoutColor = new Color(0.6f, 0.4f, 0.2f);
        [SerializeField] private Color flyoutColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color walkColor = new Color(0.3f, 0.7f, 0.9f);
        [SerializeField] private Color singleColor = new Color(0.3f, 0.8f, 0.3f);
        [SerializeField] private Color doubleColor = new Color(0.9f, 0.8f, 0.2f);
        [SerializeField] private Color tripleColor = new Color(1f, 0.5f, 0f);
        [SerializeField] private Color homerunColor = new Color(0.9f, 0.2f, 0.9f);

        [Header("Highlight")]
        [SerializeField] private Image highlightBar;
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.3f);

        private OutcomeCard currentCard;

        void Start()
        {
            if (cardContainer == null)
            {
                CreateCardLayout();
            }
        }

        private void CreateCardLayout()
        {
            // Create container
            GameObject containerObj = new GameObject("OutcomeCardContainer");
            containerObj.transform.SetParent(transform);
            
            cardContainer = containerObj.AddComponent<RectTransform>();
            cardContainer.anchorMin = new Vector2(0.5f, 0.5f);
            cardContainer.anchorMax = new Vector2(0.5f, 0.5f);
            cardContainer.sizeDelta = new Vector2(200, 300);
            cardContainer.anchoredPosition = Vector2.zero;

            // Background
            cardBackground = containerObj.AddComponent<Image>();
            cardBackground.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(containerObj.transform);
            
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.9f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            cardTitle = titleObj.AddComponent<TextMeshProUGUI>();
            cardTitle.text = "OUTCOME CARD";
            cardTitle.fontSize = 18;
            cardTitle.fontStyle = FontStyles.Bold;
            cardTitle.alignment = TextAlignmentOptions.Center;
            cardTitle.color = Color.white;

            // Rows container
            GameObject rowsObj = new GameObject("OutcomeRows");
            rowsObj.transform.SetParent(containerObj.transform);
            
            RectTransform rowsRect = rowsObj.AddComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0, 0);
            rowsRect.anchorMax = new Vector2(1, 0.88f);
            rowsRect.offsetMin = new Vector2(10, 10);
            rowsRect.offsetMax = new Vector2(-10, -5);

            outcomeRowsContainer = rowsObj.transform;

            VerticalLayoutGroup layout = rowsObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = rowSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            // Highlight bar
            GameObject highlightObj = new GameObject("Highlight");
            highlightObj.transform.SetParent(containerObj.transform);
            
            RectTransform highlightRect = highlightObj.AddComponent<RectTransform>();
            highlightRect.anchorMin = new Vector2(0, 0);
            highlightRect.anchorMax = new Vector2(1, 0);
            highlightRect.sizeDelta = new Vector2(0, rowHeight);
            highlightRect.anchoredPosition = Vector2.zero;

            highlightBar = highlightObj.AddComponent<Image>();
            highlightBar.color = highlightColor;
            highlightBar.gameObject.SetActive(false);
        }

        public void DisplayCard(OutcomeCard card, string ownerName, bool isBatter)
        {
            currentCard = card;
            
            if (cardTitle != null)
            {
                cardTitle.text = ownerName + (isBatter ? " (BATTER)" : " (PITCHER)");
            }

            ClearRows();
            
            if (card == null) return;

            CreateOutcomeRow("Strikeout", card.Strikeout, strikeoutColor);
            CreateOutcomeRow("Groundout", card.Groundout, groundoutColor);
            CreateOutcomeRow("Flyout", card.Flyout, flyoutColor);
            CreateOutcomeRow("Walk", card.Walk, walkColor);
            CreateOutcomeRow("Single", card.Single, singleColor);
            CreateOutcomeRow("Double", card.Double, doubleColor);
            CreateOutcomeRow("Triple", card.Triple, tripleColor);
            CreateOutcomeRow("Home Run", card.HomeRun, homerunColor);
        }

        private void ClearRows()
        {
            if (outcomeRowsContainer == null) return;
            
            foreach (Transform child in outcomeRowsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void CreateOutcomeRow(string outcomeName, OutcomeRange range, Color color)
        {
            if (range == null || outcomeRowsContainer == null) return;

            GameObject rowObj = new GameObject(outcomeName + "Row");
            rowObj.transform.SetParent(outcomeRowsContainer);

            RectTransform rect = rowObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, rowHeight);

            HorizontalLayoutGroup layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(5, 5, 0, 0);

            // Color indicator
            GameObject colorObj = new GameObject("Color");
            colorObj.transform.SetParent(rowObj.transform);
            
            RectTransform colorRect = colorObj.AddComponent<RectTransform>();
            colorRect.sizeDelta = new Vector2(8, rowHeight - 4);
            
            Image colorImg = colorObj.AddComponent<Image>();
            colorImg.color = color;

            LayoutElement colorLayout = colorObj.AddComponent<LayoutElement>();
            colorLayout.preferredWidth = 8;

            // Range text
            GameObject rangeObj = new GameObject("Range");
            rangeObj.transform.SetParent(rowObj.transform);

            var rangeText = rangeObj.AddComponent<TextMeshProUGUI>();
            string rangeStr = range.MinRoll == range.MaxRoll ? 
                range.MinRoll.ToString() : 
                $"{range.MinRoll}-{range.MaxRoll}";
            rangeText.text = rangeStr;
            rangeText.fontSize = 14;
            rangeText.color = Color.white;
            rangeText.alignment = TextAlignmentOptions.Left;

            LayoutElement rangeLayout = rangeObj.AddComponent<LayoutElement>();
            rangeLayout.preferredWidth = 40;

            // Outcome name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(rowObj.transform);

            var nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = outcomeName;
            nameText.fontSize = 14;
            nameText.color = color;
            nameText.alignment = TextAlignmentOptions.Left;

            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;
        }

        public void HighlightOutcome(int roll)
        {
            if (currentCard == null || highlightBar == null) return;

            AtBatOutcome outcome = currentCard.GetOutcome(roll);
            
            // Find the row to highlight
            int rowIndex = GetOutcomeRowIndex(outcome);
            if (rowIndex >= 0 && outcomeRowsContainer != null)
            {
                // Position highlight bar
                float yPos = -(rowIndex * (rowHeight + rowSpacing)) - rowHeight / 2;
                highlightBar.rectTransform.anchoredPosition = new Vector2(0, yPos);
                highlightBar.gameObject.SetActive(true);
            }
        }

        private int GetOutcomeRowIndex(AtBatOutcome outcome)
        {
            // Order matches CreateOutcomeRow calls
            return outcome switch
            {
                AtBatOutcome.Strikeout => 0,
                AtBatOutcome.Groundout => 1,
                AtBatOutcome.Flyout => 2,
                AtBatOutcome.Walk => 3,
                AtBatOutcome.Single => 4,
                AtBatOutcome.Double => 5,
                AtBatOutcome.Triple => 6,
                AtBatOutcome.HomeRun => 7,
                _ => -1
            };
        }

        public void ClearHighlight()
        {
            if (highlightBar != null)
            {
                highlightBar.gameObject.SetActive(false);
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
    }
}
