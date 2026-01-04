using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using MLBShowdown.Core;
using MLBShowdown.Network;
using MLBShowdown.Cards;

namespace MLBShowdown.UI
{
    public class GameUI : MonoBehaviour
    {
        [Header("Scoreboard")]
        [SerializeField] private TextMeshProUGUI homeScoreText;
        [SerializeField] private TextMeshProUGUI awayScoreText;
        [SerializeField] private TextMeshProUGUI inningText;
        [SerializeField] private TextMeshProUGUI outsText;

        [Header("Current At-Bat")]
        [SerializeField] private TextMeshProUGUI batterNameText;
        [SerializeField] private TextMeshProUGUI batterStatsText;
        [SerializeField] private TextMeshProUGUI pitcherNameText;
        [SerializeField] private TextMeshProUGUI pitcherStatsText;

        [Header("Base Runners")]
        [SerializeField] private Image firstBaseIndicator;
        [SerializeField] private Image secondBaseIndicator;
        [SerializeField] private Image thirdBaseIndicator;
        [SerializeField] private Color emptyBaseColor = Color.gray;
        [SerializeField] private Color occupiedBaseColor = Color.yellow;

        [Header("Game Messages")]
        [SerializeField] private TextMeshProUGUI gameMessageText;
        [SerializeField] private TextMeshProUGUI turnIndicatorText;

        [Header("Action Buttons")]
        [SerializeField] private Button rollDiceButton;
        [SerializeField] private Button attemptOptionalButton;
        [SerializeField] private Button declineOptionalButton;
        [SerializeField] private TextMeshProUGUI optionalActionText;
        [SerializeField] private GameObject optionalActionPanel;

        [Header("Dice Display")]
        [SerializeField] private TextMeshProUGUI diceResultText;
        [SerializeField] private TextMeshProUGUI advantageText;

        [Header("Outcome Charts")]
        [SerializeField] private TextMeshProUGUI pitcherChartText;
        [SerializeField] private TextMeshProUGUI batterChartText;
        [SerializeField] private GameObject pitcherChartPanel;
        [SerializeField] private GameObject batterChartPanel;

        [Header("Menu")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button vsCPUButton;
        [SerializeField] private TMP_InputField roomNameInput;

        private NetworkGameManager gameManager;
        private NetworkRunnerHandler networkHandler;
        private GameObject gameHUDPanel;

        void Start()
        {
            networkHandler = NetworkRunnerHandler.Instance;
            
            // Create UI elements if not assigned
            if (homeScoreText == null)
            {
                CreateGameHUDElements();
            }
            
            SetupMenuButtons();
            gameObject.SetActive(true); // Ensure we're active to receive updates
        }

        void Update()
        {
            if (gameManager == null)
            {
                gameManager = NetworkGameManager.Instance;
                if (gameManager != null)
                {
                    SubscribeToGameEvents();
                    ShowGameHUD();
                }
            }

            UpdateUI();
        }

        private void CreateGameHUDElements()
        {
            // Create main game HUD panel
            gameHUDPanel = new GameObject("GameHUDPanel");
            gameHUDPanel.transform.SetParent(transform);
            
            RectTransform panelRect = gameHUDPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.localScale = Vector3.one;

            // Semi-transparent background - disable raycast so it doesn't block GameboardUI
            Image bg = gameHUDPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.12f, 0.0f); // Make transparent - GameboardUI handles visuals now
            bg.raycastTarget = false;

            // Top bar - Scoreboard
            CreateScoreboard(gameHUDPanel.transform);

            // Center - Current at-bat info
            CreateAtBatDisplay(gameHUDPanel.transform);

            // Bottom - Action buttons
            CreateActionButtons(gameHUDPanel.transform);

            // Game message display
            CreateMessageDisplay(gameHUDPanel.transform);

            // Outcome charts (left and right sides)
            CreateOutcomeCharts(gameHUDPanel.transform);

            // Hide HUD initially - will show when game starts
            gameHUDPanel.SetActive(false);
        }

        private void CreateOutcomeCharts(Transform parent)
        {
            // Pitcher chart (left side)
            pitcherChartPanel = new GameObject("PitcherChartPanel");
            pitcherChartPanel.transform.SetParent(parent);
            
            RectTransform pitcherRect = pitcherChartPanel.AddComponent<RectTransform>();
            pitcherRect.anchorMin = new Vector2(0, 0.5f);
            pitcherRect.anchorMax = new Vector2(0, 0.5f);
            pitcherRect.pivot = new Vector2(0, 0.5f);
            pitcherRect.anchoredPosition = new Vector2(10, 0);
            pitcherRect.sizeDelta = new Vector2(180, 280);
            pitcherRect.localScale = Vector3.one;

            Image pitcherBg = pitcherChartPanel.AddComponent<Image>();
            pitcherBg.color = new Color(0.15f, 0.1f, 0.1f, 0.9f); // Reddish for pitcher

            CreateTextElement(pitcherChartPanel.transform, "PITCHER CHART", 14, new Vector2(90, 125));
            pitcherChartText = CreateTextElement(pitcherChartPanel.transform, "", 11, new Vector2(90, -10));
            pitcherChartText.alignment = TextAlignmentOptions.TopLeft;
            pitcherChartText.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 240);

            // Batter chart (right side)
            batterChartPanel = new GameObject("BatterChartPanel");
            batterChartPanel.transform.SetParent(parent);
            
            RectTransform batterRect = batterChartPanel.AddComponent<RectTransform>();
            batterRect.anchorMin = new Vector2(1, 0.5f);
            batterRect.anchorMax = new Vector2(1, 0.5f);
            batterRect.pivot = new Vector2(1, 0.5f);
            batterRect.anchoredPosition = new Vector2(-10, 0);
            batterRect.sizeDelta = new Vector2(180, 280);
            batterRect.localScale = Vector3.one;

            Image batterBg = batterChartPanel.AddComponent<Image>();
            batterBg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f); // Bluish for batter

            CreateTextElement(batterChartPanel.transform, "BATTER CHART", 14, new Vector2(90, 125));
            batterChartText = CreateTextElement(batterChartPanel.transform, "", 11, new Vector2(90, -10));
            batterChartText.alignment = TextAlignmentOptions.TopLeft;
            batterChartText.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 240);
        }

        private void CreateScoreboard(Transform parent)
        {
            // Scoreboard container at top
            GameObject scoreboardObj = new GameObject("Scoreboard");
            scoreboardObj.transform.SetParent(parent);
            
            RectTransform rect = scoreboardObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -10);
            rect.sizeDelta = new Vector2(0, 80);
            rect.localScale = Vector3.one;

            Image bg = scoreboardObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.15f, 0.2f, 0.95f);

            // Away score
            awayScoreText = CreateTextElement(scoreboardObj.transform, "AWAY: 0", 28, new Vector2(-200, 0));
            
            // Inning
            inningText = CreateTextElement(scoreboardObj.transform, "Top 1st", 24, new Vector2(0, 10));
            
            // Outs
            outsText = CreateTextElement(scoreboardObj.transform, "0 Outs", 20, new Vector2(0, -20));
            
            // Home score
            homeScoreText = CreateTextElement(scoreboardObj.transform, "HOME: 0", 28, new Vector2(200, 0));
        }

        private void CreateAtBatDisplay(Transform parent)
        {
            // At-bat info container
            GameObject atBatObj = new GameObject("AtBatDisplay");
            atBatObj.transform.SetParent(parent);
            
            RectTransform rect = atBatObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 50);
            rect.sizeDelta = new Vector2(600, 200);
            rect.localScale = Vector3.one;

            Image bg = atBatObj.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.18f, 0.9f);

            // Pitcher info (left side)
            pitcherNameText = CreateTextElement(atBatObj.transform, "Pitcher", 22, new Vector2(-150, 50));
            pitcherStatsText = CreateTextElement(atBatObj.transform, "Control: 0", 16, new Vector2(-150, 20));

            // VS text
            CreateTextElement(atBatObj.transform, "VS", 32, new Vector2(0, 35));

            // Batter info (right side)
            batterNameText = CreateTextElement(atBatObj.transform, "Batter", 22, new Vector2(150, 50));
            batterStatsText = CreateTextElement(atBatObj.transform, "OnBase: 0", 16, new Vector2(150, 20));

            // Dice results
            diceResultText = CreateTextElement(atBatObj.transform, "", 24, new Vector2(0, -20));
            advantageText = CreateTextElement(atBatObj.transform, "", 18, new Vector2(0, -50));

            // Base runner diamond (right side of at-bat display)
            CreateBaseRunnerDiamond(atBatObj.transform);
        }

        private void CreateBaseRunnerDiamond(Transform parent)
        {
            // Diamond container
            GameObject diamondObj = new GameObject("BaseRunnerDiamond");
            diamondObj.transform.SetParent(parent);
            
            RectTransform rect = diamondObj.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(250, 0);
            rect.sizeDelta = new Vector2(100, 100);
            rect.localScale = Vector3.one;

            float baseSize = 20f;
            float spacing = 30f;

            // Second base (top)
            secondBaseIndicator = CreateBaseIndicator(diamondObj.transform, new Vector2(0, spacing), baseSize);
            
            // Third base (left)
            thirdBaseIndicator = CreateBaseIndicator(diamondObj.transform, new Vector2(-spacing, 0), baseSize);
            
            // First base (right)
            firstBaseIndicator = CreateBaseIndicator(diamondObj.transform, new Vector2(spacing, 0), baseSize);
            
            // Home plate indicator (bottom) - just for visual reference
            CreateBaseIndicator(diamondObj.transform, new Vector2(0, -spacing), baseSize * 0.8f);
        }

        private Image CreateBaseIndicator(Transform parent, Vector2 position, float size)
        {
            GameObject baseObj = new GameObject("BaseIndicator");
            baseObj.transform.SetParent(parent);
            
            RectTransform rect = baseObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.Euler(0, 0, 45); // Diamond shape
            
            Image img = baseObj.AddComponent<Image>();
            img.color = emptyBaseColor;
            
            return img;
        }

        private void CreateActionButtons(Transform parent)
        {
            // Action buttons container at bottom
            GameObject actionsObj = new GameObject("ActionButtons");
            actionsObj.transform.SetParent(parent);
            
            RectTransform rect = actionsObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 20);
            rect.sizeDelta = new Vector2(400, 80);
            rect.localScale = Vector3.one;

            // Roll Dice button
            rollDiceButton = CreateButton(actionsObj.transform, "Roll Dice", new Vector2(0, 20), new Vector2(200, 50));
            rollDiceButton.onClick.AddListener(OnRollDiceClicked);

            // Turn indicator
            turnIndicatorText = CreateTextElement(actionsObj.transform, "Waiting...", 18, new Vector2(0, -30));
        }

        private void CreateMessageDisplay(Transform parent)
        {
            // Game message at center-bottom
            gameMessageText = CreateTextElement(parent, "", 20, new Vector2(0, -100));
            gameMessageText.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 40);
        }

        private TextMeshProUGUI CreateTextElement(Transform parent, string text, int fontSize, Vector2 position)
        {
            GameObject textObj = new GameObject("Text_" + text.Replace(" ", ""));
            textObj.transform.SetParent(parent);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(250, 40);
            rect.localScale = Vector3.one;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            
            return tmp;
        }

        private Button CreateButton(Transform parent, string text, Vector2 position, Vector2 size)
        {
            GameObject btnObj = new GameObject(text + "Button");
            btnObj.transform.SetParent(parent);
            
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.5f, 0.3f, 1f);
            
            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0.3f, 0.6f, 0.4f, 1f);
            colors.pressedColor = new Color(0.15f, 0.4f, 0.25f, 1f);
            btn.colors = colors;
            
            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.localScale = Vector3.one;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            
            return btn;
        }

        private void ShowGameHUD()
        {
            // GameboardUI now handles the main game display completely
            // Hide all GameUI elements to avoid overlap and blocking
            if (gameHUDPanel != null)
                gameHUDPanel.SetActive(false);
            
            // Also hide the entire GameUI GameObject since GameboardUI handles everything
            gameObject.SetActive(false);
        }

        private void SetupMenuButtons()
        {
            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostClicked);
            
            if (joinButton != null)
                joinButton.onClick.AddListener(OnJoinClicked);
            
            if (singlePlayerButton != null)
                singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
            
            if (vsCPUButton != null)
                vsCPUButton.onClick.AddListener(OnVsCPUClicked);

            if (rollDiceButton != null)
                rollDiceButton.onClick.AddListener(OnRollDiceClicked);

            if (attemptOptionalButton != null)
                attemptOptionalButton.onClick.AddListener(OnAttemptOptionalClicked);

            if (declineOptionalButton != null)
                declineOptionalButton.onClick.AddListener(OnDeclineOptionalClicked);
        }

        private void SubscribeToGameEvents()
        {
            if (gameManager == null) return;

            gameManager.OnGameStateChanged += HandleGameStateChanged;
            gameManager.OnScoreChanged += HandleScoreChanged;
            gameManager.OnOutsChanged += HandleOutsChanged;
            gameManager.OnInningChanged += HandleInningChanged;
            gameManager.OnGameMessage += HandleGameMessage;
        }

        void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged -= HandleGameStateChanged;
                gameManager.OnScoreChanged -= HandleScoreChanged;
                gameManager.OnOutsChanged -= HandleOutsChanged;
                gameManager.OnInningChanged -= HandleInningChanged;
                gameManager.OnGameMessage -= HandleGameMessage;
            }
        }

        #region Menu Actions

        private async void OnHostClicked()
        {
            string roomName = roomNameInput != null ? roomNameInput.text : "MLBShowdown";
            if (string.IsNullOrEmpty(roomName)) roomName = "MLBShowdown";
            
            if (networkHandler != null)
            {
                await networkHandler.HostGame(roomName);
            }
        }

        private async void OnJoinClicked()
        {
            string roomName = roomNameInput != null ? roomNameInput.text : "MLBShowdown";
            if (string.IsNullOrEmpty(roomName)) roomName = "MLBShowdown";
            
            if (networkHandler != null)
            {
                await networkHandler.JoinGame(roomName);
            }
        }

        private async void OnSinglePlayerClicked()
        {
            if (networkHandler != null)
            {
                await networkHandler.StartSinglePlayer();
            }
        }

        private async void OnVsCPUClicked()
        {
            if (networkHandler != null)
            {
                await networkHandler.StartSinglePlayer();
                // Game manager will be spawned, then we start vs CPU
                StartCoroutine(StartVsCPUAfterDelay());
            }
        }

        private System.Collections.IEnumerator StartVsCPUAfterDelay()
        {
            yield return new WaitUntil(() => NetworkGameManager.Instance != null);
            NetworkGameManager.Instance.RPC_StartGame(true, false); // CPU plays away
        }

        #endregion

        #region Game Actions

        private void OnRollDiceClicked()
        {
            if (gameManager == null) return;
            // Use local method for single player mode
            gameManager.RollDiceLocal();
        }

        private void OnAttemptOptionalClicked()
        {
            if (gameManager == null) return;
            gameManager.AttemptOptionalActionLocal();
            OnRollDiceClicked(); // Roll for the optional action
        }

        private void OnDeclineOptionalClicked()
        {
            if (gameManager == null) return;
            gameManager.DeclineOptionalActionLocal();
        }

        #endregion

        #region Event Handlers

        private void HandleGameStateChanged(GameState newState)
        {
            UpdateTurnIndicator(newState);
            UpdateButtonStates(newState);
        }

        private void HandleScoreChanged(int home, int away)
        {
            if (homeScoreText != null) homeScoreText.text = home.ToString();
            if (awayScoreText != null) awayScoreText.text = away.ToString();
        }

        private void HandleOutsChanged(int outs)
        {
            if (outsText != null) outsText.text = $"Outs: {outs}";
        }

        private void HandleInningChanged(int inning, bool isTop)
        {
            if (inningText != null)
            {
                string half = isTop ? "Top" : "Bot";
                inningText.text = $"{half} {inning}";
            }
        }

        private void HandleGameMessage(string message)
        {
            if (gameMessageText != null)
            {
                gameMessageText.text = message;
            }
            Debug.Log($"[Game] {message}");
        }

        #endregion

        #region UI Updates

        private void UpdateUI()
        {
            if (gameManager == null) return;

            UpdateScoreboard();
            UpdateCurrentAtBat();
            UpdateBaseRunners();
            UpdateDiceDisplay();
            UpdateOutcomeCharts();
        }

        private void UpdateScoreboard()
        {
            if (homeScoreText != null) homeScoreText.text = gameManager.HomeScore.ToString();
            if (awayScoreText != null) awayScoreText.text = gameManager.AwayScore.ToString();
            if (outsText != null) outsText.text = $"Outs: {gameManager.Outs}";
            
            if (inningText != null)
            {
                string half = gameManager.IsTopOfInning ? "Top" : "Bot";
                inningText.text = $"{half} {gameManager.CurrentInning}";
            }
        }

        private void UpdateCurrentAtBat()
        {
            var batter = gameManager.GetCurrentBatter();
            var pitcher = gameManager.GetCurrentPitcher();

            if (batter != null && batterNameText != null)
            {
                batterNameText.text = batter.PlayerName;
                if (batterStatsText != null)
                {
                    batterStatsText.text = $"OB: {batter.OnBase} | SPD: {batter.Speed}\n" +
                                          $"AVG: {batter.GetBattingAverage():F3}";
                }
            }

            if (pitcher != null && pitcherNameText != null)
            {
                pitcherNameText.text = pitcher.PlayerName;
                if (pitcherStatsText != null)
                {
                    pitcherStatsText.text = $"CTRL: {pitcher.Control} | IP: {pitcher.InningsPitched:F1}\n" +
                                           $"K: {pitcher.Strikeouts} | ERA: {pitcher.GetERA():F2}";
                }
            }
        }

        private void UpdateBaseRunners()
        {
            if (gameManager == null) return;

            // Get runner state from game manager (works for both local and networked)
            if (firstBaseIndicator != null)
                firstBaseIndicator.color = gameManager.HasRunnerOnFirst() ? occupiedBaseColor : emptyBaseColor;
            if (secondBaseIndicator != null)
                secondBaseIndicator.color = gameManager.HasRunnerOnSecond() ? occupiedBaseColor : emptyBaseColor;
            if (thirdBaseIndicator != null)
                thirdBaseIndicator.color = gameManager.HasRunnerOnThird() ? occupiedBaseColor : emptyBaseColor;
        }

        private void UpdateOutcomeCharts()
        {
            if (gameManager == null) return;

            var pitcher = gameManager.GetCurrentPitcher();
            var batter = gameManager.GetCurrentBatter();

            // Update pitcher chart
            if (pitcherChartText != null && pitcher != null && pitcher.OutcomeCard != null)
            {
                pitcherChartText.text = FormatOutcomeChart(pitcher.OutcomeCard, pitcher.PlayerName);
            }

            // Update batter chart
            if (batterChartText != null && batter != null && batter.OutcomeCard != null)
            {
                batterChartText.text = FormatOutcomeChart(batter.OutcomeCard, batter.PlayerName);
            }

            // Highlight the active chart based on advantage
            if (pitcherChartPanel != null && batterChartPanel != null)
            {
                var pitcherBg = pitcherChartPanel.GetComponent<Image>();
                var batterBg = batterChartPanel.GetComponent<Image>();
                
                if (gameManager.CurrentState >= GameState.OffenseTurn && 
                    gameManager.CurrentState <= GameState.AtBatAction)
                {
                    // Highlight the chart being used
                    if (gameManager.BatterHasAdvantage)
                    {
                        pitcherBg.color = new Color(0.15f, 0.1f, 0.1f, 0.6f); // Dim
                        batterBg.color = new Color(0.1f, 0.2f, 0.3f, 1f); // Bright
                    }
                    else
                    {
                        pitcherBg.color = new Color(0.25f, 0.1f, 0.1f, 1f); // Bright
                        batterBg.color = new Color(0.1f, 0.1f, 0.15f, 0.6f); // Dim
                    }
                }
                else
                {
                    // Default colors
                    pitcherBg.color = new Color(0.15f, 0.1f, 0.1f, 0.9f);
                    batterBg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
                }
            }
        }

        private string FormatOutcomeChart(OutcomeCard chart, string playerName)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{playerName}</b>\n");
            
            if (chart.Strikeout != null)
                sb.AppendLine($"K: {FormatRange(chart.Strikeout)}");
            if (chart.Groundout != null)
                sb.AppendLine($"GO: {FormatRange(chart.Groundout)}");
            if (chart.Flyout != null)
                sb.AppendLine($"FO: {FormatRange(chart.Flyout)}");
            if (chart.Walk != null)
                sb.AppendLine($"BB: {FormatRange(chart.Walk)}");
            if (chart.Single != null)
                sb.AppendLine($"1B: {FormatRange(chart.Single)}");
            if (chart.Double != null)
                sb.AppendLine($"2B: {FormatRange(chart.Double)}");
            if (chart.Triple != null)
                sb.AppendLine($"3B: {FormatRange(chart.Triple)}");
            if (chart.HomeRun != null)
                sb.AppendLine($"HR: {FormatRange(chart.HomeRun)}");
            
            return sb.ToString();
        }

        private string FormatRange(OutcomeRange range)
        {
            if (range == null) return "-";
            
            if (range.MinRoll == range.MaxRoll)
                return $"{range.MinRoll}";
            else
                return $"{range.MinRoll}-{range.MaxRoll}";
        }

        private void UpdateDiceDisplay()
        {
            if (diceResultText != null)
            {
                if (gameManager.CurrentState == GameState.OffenseTurn || 
                    gameManager.CurrentState == GameState.AtBatAction)
                {
                    diceResultText.text = $"Defense: {gameManager.DefenseRollResult}\nOffense: {gameManager.OffenseRollResult}";
                }
                else if (gameManager.CurrentState == GameState.DefenseTurn)
                {
                    diceResultText.text = $"Defense: {gameManager.DefenseRollResult}";
                }
            }

            if (advantageText != null)
            {
                if (gameManager.CurrentState >= GameState.OffenseTurn && 
                    gameManager.CurrentState <= GameState.AtBatAction)
                {
                    advantageText.text = gameManager.BatterHasAdvantage ? 
                        "BATTER ADVANTAGE" : "PITCHER ADVANTAGE";
                    advantageText.color = gameManager.BatterHasAdvantage ? 
                        Color.green : Color.red;
                }
                else
                {
                    advantageText.text = "";
                }
            }
        }

        private void UpdateTurnIndicator(GameState state)
        {
            if (turnIndicatorText == null) return;

            string turnText = state switch
            {
                GameState.WaitingForPlayers => "Waiting for players...",
                GameState.RollForTeamAssignment => "Roll to determine teams!",
                GameState.SetLineups => "Setting lineups...",
                GameState.StartGame => "Play ball!",
                GameState.DefenseTurn => "Defense: Roll for advantage",
                GameState.OffenseTurn => "Offense: Roll for outcome",
                GameState.AtBatAction => "Resolving at-bat...",
                GameState.OptionalAction => GetOptionalActionText(),
                GameState.UpdateBaseRunners => "Updating runners...",
                GameState.NextBatterUp => "Next batter...",
                GameState.NewHalfInning => "New half inning",
                GameState.EndHalfInning => "Side retired",
                GameState.GameOver => "GAME OVER",
                _ => ""
            };

            turnIndicatorText.text = turnText;
        }

        private string GetOptionalActionText()
        {
            if (gameManager == null) return "";

            return gameManager.AvailableOptionalAction switch
            {
                OptionalActionType.StolenBase => "Attempt stolen base?",
                OptionalActionType.TagUp => "Attempt tag up (sac fly)?",
                OptionalActionType.DoublePlay => "Defense: Attempt double play?",
                _ => ""
            };
        }

        private void UpdateButtonStates(GameState state)
        {
            bool canRoll = state == GameState.DefenseTurn || 
                          state == GameState.OffenseTurn ||
                          state == GameState.RollForTeamAssignment;

            if (rollDiceButton != null)
                rollDiceButton.gameObject.SetActive(canRoll);

            bool showOptional = state == GameState.OptionalAction;
            if (optionalActionPanel != null)
                optionalActionPanel.SetActive(showOptional);

            if (showOptional && optionalActionText != null)
            {
                optionalActionText.text = GetOptionalActionText();
            }
        }

        private void ShowMenu()
        {
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        private void HideMenu()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
        }

        private void ShowGameUI()
        {
            // Enable all game UI elements
        }

        private void HideGameUI()
        {
            // Disable all game UI elements until game starts
        }

        #endregion
    }
}
