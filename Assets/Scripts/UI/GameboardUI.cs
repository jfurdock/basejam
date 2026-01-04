using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using MLBShowdown.Core;
using MLBShowdown.Network;
using MLBShowdown.Cards;

namespace MLBShowdown.UI
{
    /// <summary>
    /// Visual gameboard UI that displays the baseball field with lineups and moving cards.
    /// Resembles the classic MLB Showdown physical game board.
    /// Layout: Opponent lineup (top) | Field (center) | Local player lineup (bottom)
    /// </summary>
    public class GameboardUI : MonoBehaviour
    {
        [Header("Main Panels")]
        [SerializeField] private GameObject gameboardPanel;
        [SerializeField] private RectTransform fieldArea;
        
        [Header("Lineup Areas")]
        [SerializeField] private RectTransform topLineupArea;    // Opponent
        [SerializeField] private RectTransform bottomLineupArea; // Local player
        
        [Header("Field Positions")]
        [SerializeField] private RectTransform homePlatePosition;
        [SerializeField] private RectTransform firstBasePosition;
        [SerializeField] private RectTransform secondBasePosition;
        [SerializeField] private RectTransform thirdBasePosition;
        [SerializeField] private RectTransform pitcherMoundPosition;
        
        [Header("Scorebug")]
        [SerializeField] private GameObject scorebugPanel;
        [SerializeField] private TextMeshProUGUI awayScoreText;
        [SerializeField] private TextMeshProUGUI homeScoreText;
        [SerializeField] private TextMeshProUGUI inningText;
        [SerializeField] private TextMeshProUGUI outsText;
        [SerializeField] private Image[] outIndicators;
        
        [Header("Game Controls")]
        [SerializeField] private Button rollDiceButton;
        [SerializeField] private TextMeshProUGUI turnIndicatorText;
        [SerializeField] private TextMeshProUGUI gameMessageText;
        
        [Header("Outcome Charts")]
        [SerializeField] private GameObject pitcherChartPanel;
        [SerializeField] private GameObject batterChartPanel;
        [SerializeField] private TextMeshProUGUI pitcherChartText;
        [SerializeField] private TextMeshProUGUI batterChartText;
        
        [Header("Card Prefab Settings")]
        [SerializeField] private Vector2 cardSize = new Vector2(120, 150);
        [SerializeField] private Vector2 lineupCardSize = new Vector2(100, 120);
        [SerializeField] private float cardMoveSpeed = 8f;
        [SerializeField] private float baseRunningDelay = 0.3f;
        
        [Header("Colors")]
        [SerializeField] private Color awayTeamColor = new Color(0.2f, 0.3f, 0.6f, 1f);
        [SerializeField] private Color homeTeamColor = new Color(0.6f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color fieldGreenColor = new Color(0.2f, 0.5f, 0.2f, 1f);
        [SerializeField] private Color dirtColor = new Color(0.6f, 0.4f, 0.3f, 1f);
        [SerializeField] private Color activeCardColor = new Color(1f, 0.9f, 0.5f, 1f);
        [SerializeField] private Color advantageColor = new Color(0.3f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color noAdvantageColor = new Color(0.4f, 0.4f, 0.45f, 0.9f);
        
        // Track if local player is home team
        private bool localPlayerIsHome = true;

        // Card UI elements
        private GameObject[] awayLineupCards = new GameObject[9];
        private GameObject[] homeLineupCards = new GameObject[9];
        private GameObject awayPitcherCard;
        private GameObject homePitcherCard;
        
        // Cards on field (batter and baserunners)
        private GameObject batterCard;
        private GameObject firstBaseCard;
        private GameObject secondBaseCard;
        private GameObject thirdBaseCard;
        private GameObject pitcherOnMoundCard;
        
        // Runner tracking - store batter data for each base
        private class RunnerInfo
        {
            public string PlayerName;
            public int Speed;
            public int LineupIndex;      // Which lineup slot this runner came from
            public bool IsAwayTeam;      // Which team's lineup
            public GameObject Card;      // The card GameObject on the field
        }
        private RunnerInfo runnerOnFirst;
        private RunnerInfo runnerOnSecond;
        private RunnerInfo runnerOnThird;
        private RunnerInfo currentBatterInfo;
        
        // Pitcher tracking
        private bool currentPitcherIsHome; // Which team's pitcher is on mound
        
        // Dice display
        private TextMeshProUGUI diceResultText;
        private GameObject diceDisplayPanel;
        private Coroutine diceAnimationCoroutine;
        
        // 3D Dice roller
        private MLBShowdown.Dice.DiceRollerUI diceRollerUI;
        private bool pitcherOnMound = false; // Is there a pitcher currently on the mound
        private bool isAnimatingPitcher = false; // Prevent re-entry during animation
        
        // Track which lineup slots have players on the field (to hide them in lineup)
        private HashSet<int> awayPlayersOnField = new HashSet<int>();
        private HashSet<int> homePlayersOnField = new HashSet<int>();
        
        // Helper to get lineup cards for a team, accounting for UI swap (user always on bottom)
        private GameObject[] GetLineupCardsForTeam(bool isAwayTeam)
        {
            bool userIsAway = IsLocalPlayerAway();
            // If user is away: away team uses bottom (homeLineupCards), home team uses top (awayLineupCards)
            // If user is home: away team uses top (awayLineupCards), home team uses bottom (homeLineupCards)
            if (userIsAway)
            {
                return isAwayTeam ? homeLineupCards : awayLineupCards;
            }
            else
            {
                return isAwayTeam ? awayLineupCards : homeLineupCards;
            }
        }
        
        /// <summary>
        /// Returns true if the local player is the Away team.
        /// Works for both CPU games and multiplayer.
        /// </summary>
        private bool IsLocalPlayerAway()
        {
            if (gameManager == null) return false;
            
            // In CPU game, player is away if CPU is home
            if (gameManager.IsCPUGame)
            {
                return gameManager.CPUIsHome;
            }
            
            // In multiplayer, check if local player is the away player
            return !gameManager.IsLocalPlayerHome();
        }
        
        private HashSet<int> GetPlayersOnFieldForTeam(bool isAwayTeam)
        {
            return isAwayTeam ? awayPlayersOnField : homePlayersOnField;
        }
        
        // Helper to get pitcher lineup card for a team, accounting for UI swap
        // awayPitcherCard = top lineup area, homePitcherCard = bottom lineup area
        // User is always on bottom, opponent on top
        private GameObject GetPitcherCardForTeam(bool isHomePitchingTeam)
        {
            bool userIsAway = IsLocalPlayerAway();
            // When user is away:
            //   - User (away team) pitcher is in BOTTOM (homePitcherCard)
            //   - Opponent (home team) pitcher is in TOP (awayPitcherCard)
            // When user is home:
            //   - User (home team) pitcher is in BOTTOM (homePitcherCard)
            //   - Opponent (away team) pitcher is in TOP (awayPitcherCard)
            // 
            // So: user's pitcher is ALWAYS in homePitcherCard (bottom)
            //     opponent's pitcher is ALWAYS in awayPitcherCard (top)
            //
            // isHomePitchingTeam = true means home team is pitching
            // If userIsAway: home team = opponent → return awayPitcherCard (top)
            // If !userIsAway: home team = user → return homePitcherCard (bottom)
            if (userIsAway)
            {
                // Home team = opponent, Away team = user
                return isHomePitchingTeam ? awayPitcherCard : homePitcherCard;
            }
            else
            {
                // Home team = user, Away team = opponent
                return isHomePitchingTeam ? homePitcherCard : awayPitcherCard;
            }
        }
        
        // Animation state
        private bool isAnimatingRunners = false;
        private Queue<System.Action> animationQueue = new Queue<System.Action>();
        
        // Target positions for smooth movement
        private Vector3 batterTargetPos;
        private Vector3 firstBaseTargetPos;
        private Vector3 secondBaseTargetPos;
        private Vector3 thirdBaseTargetPos;
        
        // Base positions for animation
        private Vector2 homePos;
        private Vector2 firstPos;
        private Vector2 secondPos;
        private Vector2 thirdPos;
        
        // References
        private NetworkGameManager gameManager;
        private Canvas canvas;

        void Start()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
            
            CreateGameboard();
            
            // Subscribe to game events
            if (NetworkGameManager.Instance != null)
            {
                gameManager = NetworkGameManager.Instance;
                SubscribeToEvents();
            }
        }

        void Update()
        {
            // Find game manager if not set, or re-subscribe if instance changed
            var currentInstance = NetworkGameManager.Instance;
            if (currentInstance != null && currentInstance != gameManager)
            {
                // Unsubscribe from old instance if any
                if (gameManager != null)
                {
                    UnsubscribeFromEvents();
                }
                
                gameManager = currentInstance;
                SubscribeToEvents();
                RefreshBoard();
            }
            
            // Smooth card movement
            UpdateCardPositions();
        }
        
        private void UnsubscribeFromEvents()
        {
            if (gameManager == null) return;
            gameManager.OnGameStarted -= OnGameStarted;
            gameManager.OnAtBatStarted -= OnAtBatStarted;
            gameManager.OnAtBatEnded -= OnAtBatEnded;
            gameManager.OnScoreChanged -= OnScoreChanged;
            gameManager.OnInningChanged -= OnInningChanged;
            gameManager.OnGameMessage -= OnGameMessage;
            gameManager.OnGameStateChanged -= OnGameStateChanged;
            
            // Unsubscribe from dice roll events
            var diceRoller = FindObjectOfType<MLBShowdown.Dice.DiceRoller3D>();
            if (diceRoller != null)
            {
                diceRoller.OnDiceRollStarted -= OnDiceRollStarted;
                diceRoller.OnDiceRollComplete -= OnDiceRollComplete;
            }
        }

        private void SubscribeToEvents()
        {
            if (gameManager == null) return;
            Debug.Log($"[GameboardUI] SubscribeToEvents - subscribing to gameManager instance {gameManager.GetInstanceID()}");
            gameManager.OnGameStarted += OnGameStarted;
            gameManager.OnAtBatStarted += OnAtBatStarted;
            gameManager.OnAtBatEnded += OnAtBatEnded;
            gameManager.OnScoreChanged += OnScoreChanged;
            gameManager.OnInningChanged += OnInningChanged;
            gameManager.OnGameMessage += OnGameMessage;
            gameManager.OnGameStateChanged += OnGameStateChanged;
            
            // Subscribe to dice roll events
            var diceRoller = FindObjectOfType<MLBShowdown.Dice.DiceRoller3D>();
            if (diceRoller != null)
            {
                diceRoller.OnDiceRollStarted += OnDiceRollStarted;
                diceRoller.OnDiceRollComplete += OnDiceRollComplete;
            }
        }

        void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        private void CreateGameboard()
        {
            // Main gameboard panel
            gameboardPanel = new GameObject("GameboardPanel");
            gameboardPanel.transform.SetParent(transform);
            
            RectTransform panelRect = gameboardPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.localScale = Vector3.one;
            
            Image panelBg = gameboardPanel.AddComponent<Image>();
            panelBg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            // Create the layout: Opponent (top) | Charts + Field + Controls (center) | Local Player (bottom)
            CreateTopLineupArea();      // Opponent
            CreateFieldArea();          // Center with diamond
            CreateBottomLineupArea();   // Local player
            CreateScorebug();           // Score, inning, outs
            CreateGameControls();       // Roll button, messages
            CreateOutcomeCharts();      // Pitcher and batter charts on sides
            
            // Initially hide until game starts
            gameboardPanel.SetActive(false);
        }

        private void CreateTopLineupArea()
        {
            // Opponent lineup at top (horizontal layout) - full width
            GameObject topArea = new GameObject("TopLineupArea");
            topArea.transform.SetParent(gameboardPanel.transform);
            
            topLineupArea = topArea.AddComponent<RectTransform>();
            topLineupArea.anchorMin = new Vector2(0, 0.84f);
            topLineupArea.anchorMax = new Vector2(1, 1);
            topLineupArea.offsetMin = new Vector2(5, 3);
            topLineupArea.offsetMax = new Vector2(-5, -3);
            topLineupArea.localScale = Vector3.one;
            
            Image bg = topArea.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.2f, 0.2f, 0.85f); // Slightly red for opponent

            // Label - Opponent is always on top
            CreateTextElement(topArea.transform, "OPPONENT", 18, new Vector2(-480, 0), TextAnchor.MiddleLeft);

            // Horizontal layout for 9 batters + pitcher - spread across full width
            float cardWidth = lineupCardSize.x + 8;
            float pitcherGap = 30f; // Extra gap between pitcher and batters
            float startX = -((cardWidth * 10 + pitcherGap) / 2) + cardWidth / 2 + 40;
            
            // Pitcher on left
            awayPitcherCard = CreateCardSlot(topArea.transform, "P", 
                new Vector2(startX, 0), lineupCardSize, true);
            
            // 9 batter slots (with gap after pitcher)
            for (int i = 0; i < 9; i++)
            {
                Vector2 pos = new Vector2(startX + pitcherGap + (i + 1) * cardWidth, 0);
                awayLineupCards[i] = CreateCardSlot(topArea.transform, $"{i + 1}", pos, lineupCardSize, true);
            }
        }

        private void CreateBottomLineupArea()
        {
            // Local player lineup at bottom (horizontal layout) - full width
            GameObject bottomArea = new GameObject("BottomLineupArea");
            bottomArea.transform.SetParent(gameboardPanel.transform);
            
            bottomLineupArea = bottomArea.AddComponent<RectTransform>();
            bottomLineupArea.anchorMin = new Vector2(0, 0);
            bottomLineupArea.anchorMax = new Vector2(1, 0.16f);
            bottomLineupArea.offsetMin = new Vector2(5, 3);
            bottomLineupArea.offsetMax = new Vector2(-5, -3);
            bottomLineupArea.localScale = Vector3.one;
            
            Image bg = bottomArea.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.28f, 0.85f); // Slightly blue for local player

            // Label - User's team is always on bottom
            CreateTextElement(bottomArea.transform, "YOUR TEAM", 18, new Vector2(-480, 0), TextAnchor.MiddleLeft);

            // Horizontal layout for 9 batters + pitcher - spread across full width
            float cardWidth = lineupCardSize.x + 8;
            float pitcherGap = 30f; // Extra gap between pitcher and batters
            float startX = -((cardWidth * 10 + pitcherGap) / 2) + cardWidth / 2 + 40;
            
            // Pitcher on left
            homePitcherCard = CreateCardSlot(bottomArea.transform, "P", 
                new Vector2(startX, 0), lineupCardSize, true);
            
            // 9 batter slots (with gap after pitcher)
            for (int i = 0; i < 9; i++)
            {
                Vector2 pos = new Vector2(startX + pitcherGap + (i + 1) * cardWidth, 0);
                homeLineupCards[i] = CreateCardSlot(bottomArea.transform, $"{i + 1}", pos, lineupCardSize, true);
            }
        }

        private void CreateScorebug()
        {
            // Scorebug in top-left corner of field area
            scorebugPanel = new GameObject("Scorebug");
            scorebugPanel.transform.SetParent(gameboardPanel.transform);
            
            RectTransform rect = scorebugPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.84f);
            rect.anchorMax = new Vector2(0.18f, 0.98f);
            rect.offsetMin = new Vector2(5, 5);
            rect.offsetMax = new Vector2(-5, -5);
            rect.localScale = Vector3.one;
            
            // Ensure scorebug is on top
            scorebugPanel.transform.SetAsLastSibling();
            
            Image bg = scorebugPanel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            // Team labels and scores
            // Away row
            CreateScoreText(scorebugPanel.transform, "AWAY", new Vector2(15, -15), 22, TextAlignmentOptions.Left);
            awayScoreText = CreateScoreText(scorebugPanel.transform, "0", new Vector2(220, -15), 28, TextAlignmentOptions.Right);
            
            // Home row
            CreateScoreText(scorebugPanel.transform, "HOME", new Vector2(15, -50), 22, TextAlignmentOptions.Left);
            homeScoreText = CreateScoreText(scorebugPanel.transform, "0", new Vector2(220, -50), 28, TextAlignmentOptions.Right);
            
            // Inning
            inningText = CreateScoreText(scorebugPanel.transform, "▲ 1", new Vector2(130, -85), 24, TextAlignmentOptions.Center);
            
            // Outs indicators
            outsText = CreateScoreText(scorebugPanel.transform, "0 OUT", new Vector2(220, -85), 22, TextAlignmentOptions.Right);
            
            // Create out indicator circles
            outIndicators = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject outObj = new GameObject($"Out{i + 1}");
                outObj.transform.SetParent(scorebugPanel.transform);
                
                RectTransform outRect = outObj.AddComponent<RectTransform>();
                outRect.anchorMin = new Vector2(0, 1);
                outRect.anchorMax = new Vector2(0, 1);
                outRect.sizeDelta = new Vector2(18, 18);
                outRect.anchoredPosition = new Vector2(20 + i * 24, -90);
                outRect.localScale = Vector3.one;
                
                outIndicators[i] = outObj.AddComponent<Image>();
                outIndicators[i].color = new Color(0.3f, 0.3f, 0.3f, 1f);
            }
        }

        private TextMeshProUGUI CreateScoreText(Transform parent, string text, Vector2 pos, int fontSize, TextAlignmentOptions align)
        {
            GameObject textObj = new GameObject("ScoreText");
            textObj.transform.SetParent(parent);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(80, 25);
            rect.localScale = Vector3.one;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            
            return tmp;
        }

        private void CreateGameControls()
        {
            // Roll button and turn indicator - positioned in the batter chart area (bottom portion)
            GameObject controlsPanel = new GameObject("GameControls");
            controlsPanel.transform.SetParent(gameboardPanel.transform);
            
            RectTransform rect = controlsPanel.AddComponent<RectTransform>();
            // Position in lower right, below the batter chart
            rect.anchorMin = new Vector2(0.82f, 0.16f);
            rect.anchorMax = new Vector2(1f, 0.50f);
            rect.offsetMin = new Vector2(5, 5);
            rect.offsetMax = new Vector2(-5, -5);
            rect.localScale = Vector3.one;
            
            Image bg = controlsPanel.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            bg.raycastTarget = false; // Don't block child button clicks

            // Turn indicator at top of controls panel
            GameObject turnObj = new GameObject("TurnIndicator");
            turnObj.transform.SetParent(controlsPanel.transform);
            
            RectTransform turnRect = turnObj.AddComponent<RectTransform>();
            turnRect.anchorMin = new Vector2(0, 0.7f);
            turnRect.anchorMax = new Vector2(1, 1f);
            turnRect.offsetMin = new Vector2(5, 5);
            turnRect.offsetMax = new Vector2(-5, -5);
            turnRect.localScale = Vector3.one;
            
            turnIndicatorText = turnObj.AddComponent<TextMeshProUGUI>();
            turnIndicatorText.text = "YOUR TURN";
            turnIndicatorText.fontSize = 22;
            turnIndicatorText.alignment = TextAlignmentOptions.Center;
            turnIndicatorText.color = Color.yellow;
            turnIndicatorText.fontStyle = FontStyles.Bold;
            turnIndicatorText.raycastTarget = false;
            
            // Roll button - center of controls panel (larger hit area)
            GameObject buttonObj = new GameObject("RollButton");
            buttonObj.transform.SetParent(controlsPanel.transform);
            
            RectTransform btnRect = buttonObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.05f, 0.25f);
            btnRect.anchorMax = new Vector2(0.95f, 0.70f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;
            btnRect.localScale = Vector3.one;
            
            Image btnBg = buttonObj.AddComponent<Image>();
            btnBg.color = new Color(0.2f, 0.6f, 0.3f, 1f);
            btnBg.raycastTarget = true;
            
            rollDiceButton = buttonObj.AddComponent<Button>();
            rollDiceButton.targetGraphic = btnBg;
            rollDiceButton.onClick.AddListener(OnRollButtonClicked);
            
            // Set button colors for visual feedback
            var colors = rollDiceButton.colors;
            colors.normalColor = new Color(0.2f, 0.6f, 0.3f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.7f, 0.4f, 1f);
            colors.pressedColor = new Color(0.1f, 0.4f, 0.2f, 1f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            rollDiceButton.colors = colors;
            
            // Button text
            GameObject btnTextObj = new GameObject("ButtonText");
            btnTextObj.transform.SetParent(buttonObj.transform);
            
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            btnTextRect.localScale = Vector3.one;
            
            TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "ROLL DICE";
            btnText.fontSize = 32;
            btnText.raycastTarget = false; // Don't block button clicks
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;
            btnText.fontStyle = FontStyles.Bold;
            
            // Game message - bottom of controls panel
            GameObject msgObj = new GameObject("GameMessage");
            msgObj.transform.SetParent(controlsPanel.transform);
            
            RectTransform msgRect = msgObj.AddComponent<RectTransform>();
            msgRect.anchorMin = new Vector2(0, 0);
            msgRect.anchorMax = new Vector2(1, 0.35f);
            msgRect.offsetMin = new Vector2(5, 5);
            msgRect.offsetMax = new Vector2(-5, -5);
            msgRect.localScale = Vector3.one;
            
            gameMessageText = msgObj.AddComponent<TextMeshProUGUI>();
            gameMessageText.text = "";
            gameMessageText.fontSize = 14;
            gameMessageText.alignment = TextAlignmentOptions.Center;
            gameMessageText.color = Color.white;
            gameMessageText.enableWordWrapping = true;
            gameMessageText.raycastTarget = false;
            
            // Create 3D dice roller
            Create3DDiceRoller();
        }
        
        private void Create3DDiceRoller()
        {
            // Create the 3D dice roller component
            GameObject diceRollerObj = new GameObject("DiceRollerUI");
            diceRollerUI = diceRollerObj.AddComponent<MLBShowdown.Dice.DiceRollerUI>();
            
            // Subscribe to dice events
            diceRollerUI.OnDiceRollStarted += OnDiceRollStarted;
            diceRollerUI.OnDiceRollComplete += OnDiceRollComplete;
        }
        
        private void CreateDiceDisplay()
        {
            // Create dice display panel in the center of the field
            diceDisplayPanel = new GameObject("DiceDisplayPanel");
            diceDisplayPanel.transform.SetParent(fieldArea);
            
            RectTransform diceRect = diceDisplayPanel.AddComponent<RectTransform>();
            diceRect.anchorMin = new Vector2(0.35f, 0.35f);
            diceRect.anchorMax = new Vector2(0.65f, 0.65f);
            diceRect.offsetMin = Vector2.zero;
            diceRect.offsetMax = Vector2.zero;
            diceRect.localScale = Vector3.one;
            
            // Semi-transparent background
            Image diceBg = diceDisplayPanel.AddComponent<Image>();
            diceBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            diceBg.raycastTarget = false;
            
            // Dice result text
            GameObject diceTextObj = new GameObject("DiceResultText");
            diceTextObj.transform.SetParent(diceDisplayPanel.transform);
            
            RectTransform diceTextRect = diceTextObj.AddComponent<RectTransform>();
            diceTextRect.anchorMin = Vector2.zero;
            diceTextRect.anchorMax = Vector2.one;
            diceTextRect.offsetMin = Vector2.zero;
            diceTextRect.offsetMax = Vector2.zero;
            diceTextRect.localScale = Vector3.one;
            
            diceResultText = diceTextObj.AddComponent<TextMeshProUGUI>();
            diceResultText.text = "";
            diceResultText.fontSize = 72;
            diceResultText.alignment = TextAlignmentOptions.Center;
            diceResultText.color = Color.white;
            diceResultText.fontStyle = FontStyles.Bold;
            diceResultText.raycastTarget = false;
            
            // Hide initially
            diceDisplayPanel.SetActive(false);
        }

        private void OnRollButtonClicked()
        {
            Debug.Log("[GameboardUI] Roll button clicked!");
            if (gameManager != null)
            {
                // Check if it's the local player's turn
                if (!gameManager.IsLocalPlayerTurn())
                {
                    Debug.Log("[GameboardUI] Not local player's turn - ignoring roll request");
                    return;
                }
                
                Debug.Log($"[GameboardUI] Requesting dice roll. State: {gameManager.CurrentState}, IsTopOfInning: {gameManager.IsTopOfInning}");
                
                // Show 3D dice animation if available
                if (diceRollerUI != null && !diceRollerUI.IsRolling)
                {
                    diceRollerUI.RollDice();
                }
                
                // Always use the game manager's dice roll flow (handles multiplayer RPC)
                gameManager.RequestDiceRoll();
            }
            else
            {
                Debug.LogWarning("[GameboardUI] Game manager is null!");
            }
        }

        private void CreateFieldArea()
        {
            // Central field area with diamond - leave room for charts on sides
            GameObject field = new GameObject("FieldArea");
            field.transform.SetParent(gameboardPanel.transform);
            
            fieldArea = field.AddComponent<RectTransform>();
            fieldArea.anchorMin = new Vector2(0.18f, 0.16f);
            fieldArea.anchorMax = new Vector2(0.82f, 0.84f);
            fieldArea.offsetMin = new Vector2(5, 5);
            fieldArea.offsetMax = new Vector2(-5, -5);
            fieldArea.localScale = Vector3.one;
            
            // Green grass background
            Image fieldBg = field.AddComponent<Image>();
            fieldBg.color = fieldGreenColor;

            // Create the diamond
            CreateDiamond();
            
            // Create base positions
            CreateBasePositions();
            
            // Create pitcher mound
            CreatePitcherMound();
            
            // Store base positions for animation
            float diamondRadius = 100f;
            Vector2 center = new Vector2(0, 0);
            homePos = center + new Vector2(0, -diamondRadius - 40);
            firstPos = center + new Vector2(diamondRadius + 25, 0);
            secondPos = center + new Vector2(0, diamondRadius + 25);
            thirdPos = center + new Vector2(-diamondRadius - 25, 0);
        }

        private void CreateOutcomeCharts()
        {
            // Pitcher chart on left side
            pitcherChartPanel = new GameObject("PitcherChartPanel");
            pitcherChartPanel.transform.SetParent(gameboardPanel.transform);
            
            RectTransform pitcherRect = pitcherChartPanel.AddComponent<RectTransform>();
            pitcherRect.anchorMin = new Vector2(0, 0.16f);
            pitcherRect.anchorMax = new Vector2(0.18f, 0.84f);
            pitcherRect.offsetMin = new Vector2(5, 5);
            pitcherRect.offsetMax = new Vector2(-5, -5);
            pitcherRect.localScale = Vector3.one;

            Image pitcherBg = pitcherChartPanel.AddComponent<Image>();
            pitcherBg.color = noAdvantageColor;
            pitcherBg.raycastTarget = false;

            // Pitcher chart title
            CreateChartTitle(pitcherChartPanel.transform, "PITCHER CHART", new Vector2(0, -15));
            
            // Pitcher chart content
            pitcherChartText = CreateChartText(pitcherChartPanel.transform, "", new Vector2(0, -40));

            // Batter chart on right side
            batterChartPanel = new GameObject("BatterChartPanel");
            batterChartPanel.transform.SetParent(gameboardPanel.transform);
            
            RectTransform batterRect = batterChartPanel.AddComponent<RectTransform>();
            batterRect.anchorMin = new Vector2(0.82f, 0.50f);  // Start above controls panel
            batterRect.anchorMax = new Vector2(1, 0.84f);
            batterRect.offsetMin = new Vector2(5, 5);
            batterRect.offsetMax = new Vector2(-5, -5);
            batterRect.localScale = Vector3.one;

            Image batterBg = batterChartPanel.AddComponent<Image>();
            batterBg.color = noAdvantageColor;
            batterBg.raycastTarget = false; // Don't block controls below

            // Batter chart title
            CreateChartTitle(batterChartPanel.transform, "BATTER CHART", new Vector2(0, -15));
            
            // Batter chart content
            batterChartText = CreateChartText(batterChartPanel.transform, "", new Vector2(0, -40));
        }

        private void CreateChartTitle(Transform parent, string title, Vector2 pos)
        {
            GameObject titleObj = new GameObject("ChartTitle");
            titleObj.transform.SetParent(parent);
            
            RectTransform rect = titleObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(180, 35);
            rect.localScale = Vector3.one;
            
            TextMeshProUGUI tmp = titleObj.AddComponent<TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
        }

        private TextMeshProUGUI CreateChartText(Transform parent, string text, Vector2 pos)
        {
            GameObject textObj = new GameObject("ChartText");
            textObj.transform.SetParent(parent);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(0, 400);
            rect.localScale = Vector3.one;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.margin = new Vector4(10, 0, 10, 0);
            
            return tmp;
        }

        private void CreateDiamond()
        {
            // Infield dirt area (rotated square)
            GameObject infield = new GameObject("Infield");
            infield.transform.SetParent(fieldArea);
            
            RectTransform infieldRect = infield.AddComponent<RectTransform>();
            infieldRect.anchorMin = new Vector2(0.5f, 0.5f);
            infieldRect.anchorMax = new Vector2(0.5f, 0.5f);
            infieldRect.sizeDelta = new Vector2(300, 300);
            infieldRect.anchoredPosition = new Vector2(0, -30);
            infieldRect.localScale = Vector3.one;
            infieldRect.localRotation = Quaternion.Euler(0, 0, 45);
            
            Image infieldBg = infield.AddComponent<Image>();
            infieldBg.color = dirtColor;

            // Base paths (white lines) - simplified as the rotated square edges
            GameObject basePaths = new GameObject("BasePaths");
            basePaths.transform.SetParent(infield.transform);
            
            RectTransform pathsRect = basePaths.AddComponent<RectTransform>();
            pathsRect.anchorMin = Vector2.zero;
            pathsRect.anchorMax = Vector2.one;
            pathsRect.offsetMin = new Vector2(5, 5);
            pathsRect.offsetMax = new Vector2(-5, -5);
            pathsRect.localScale = Vector3.one;
            
            // Inner grass
            Image innerGrass = basePaths.AddComponent<Image>();
            innerGrass.color = new Color(fieldGreenColor.r * 0.9f, fieldGreenColor.g * 0.9f, fieldGreenColor.b * 0.9f, 1f);
        }

        private void CreateBasePositions()
        {
            float baseSize = 40f;
            float diamondRadius = 120f;
            Vector2 center = new Vector2(0, -30);

            // Home plate (bottom)
            homePlatePosition = CreateBaseMarker(fieldArea, "HomePlate", 
                center + new Vector2(0, -diamondRadius), baseSize, Color.white);
            
            // First base (right)
            firstBasePosition = CreateBaseMarker(fieldArea, "FirstBase", 
                center + new Vector2(diamondRadius, 0), baseSize, Color.white);
            
            // Second base (top)
            secondBasePosition = CreateBaseMarker(fieldArea, "SecondBase", 
                center + new Vector2(0, diamondRadius), baseSize, Color.white);
            
            // Third base (left)
            thirdBasePosition = CreateBaseMarker(fieldArea, "ThirdBase", 
                center + new Vector2(-diamondRadius, 0), baseSize, Color.white);
        }

        private RectTransform CreateBaseMarker(Transform parent, string name, Vector2 position, float size, Color color)
        {
            GameObject baseObj = new GameObject(name);
            baseObj.transform.SetParent(parent);
            
            RectTransform rect = baseObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.Euler(0, 0, 45);
            
            Image img = baseObj.AddComponent<Image>();
            img.color = color;
            
            return rect;
        }

        private void CreatePitcherMound()
        {
            GameObject mound = new GameObject("PitcherMound");
            mound.transform.SetParent(fieldArea);
            
            pitcherMoundPosition = mound.AddComponent<RectTransform>();
            pitcherMoundPosition.anchorMin = new Vector2(0.5f, 0.5f);
            pitcherMoundPosition.anchorMax = new Vector2(0.5f, 0.5f);
            pitcherMoundPosition.sizeDelta = new Vector2(50, 50);
            pitcherMoundPosition.anchoredPosition = new Vector2(0, -30);
            pitcherMoundPosition.localScale = Vector3.one;
            
            Image moundImg = mound.AddComponent<Image>();
            moundImg.color = dirtColor;
            
            // Make it circular
            // (In a real implementation, you'd use a circular sprite)
        }

        private GameObject CreateCardSlot(Transform parent, string name, Vector2 position, Vector2 size, bool horizontalLayout = false)
        {
            GameObject slot = new GameObject(name);
            slot.transform.SetParent(parent);
            
            RectTransform rect = slot.AddComponent<RectTransform>();
            if (horizontalLayout)
            {
                // Center anchor for horizontal lineup
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
            }
            else
            {
                // Top anchor for vertical lineup
                rect.anchorMin = new Vector2(0.5f, 1);
                rect.anchorMax = new Vector2(0.5f, 1);
            }
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            
            Image bg = slot.AddComponent<Image>();
            bg.color = new Color(0.3f, 0.3f, 0.35f, 0.8f);
            
            // Card name text
            GameObject textObj = new GameObject("CardText");
            textObj.transform.SetParent(slot.transform);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(2, 2);
            textRect.offsetMax = new Vector2(-2, -2);
            textRect.localScale = Vector3.one;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            
            return slot;
        }

        private GameObject CreateFieldCard(Transform parent, string name, Vector2 position)
        {
            GameObject card = new GameObject(name);
            card.transform.SetParent(parent);
            
            RectTransform rect = card.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = cardSize;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            
            Image bg = card.AddComponent<Image>();
            bg.color = new Color(0.9f, 0.85f, 0.7f, 1f);
            
            // Card content
            GameObject textObj = new GameObject("CardText");
            textObj.transform.SetParent(card.transform);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(3, 3);
            textRect.offsetMax = new Vector2(-3, -3);
            textRect.localScale = Vector3.one;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            tmp.enableWordWrapping = true;
            
            card.SetActive(false);
            return card;
        }

        private TextMeshProUGUI CreateTextElement(Transform parent, string text, int fontSize, Vector2 position, TextAnchor anchor)
        {
            GameObject textObj = new GameObject("Text_" + text);
            textObj.transform.SetParent(parent);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(150, 30);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            
            return tmp;
        }

        #region Game Event Handlers

        private void OnGameStarted()
        {
            gameboardPanel.SetActive(true);
            RefreshBoard();
        }

        private void OnAtBatStarted(int batterIndex)
        {
            // Queue the next batter - wait for any current animation to finish first
            StartCoroutine(WaitAndBringUpNextBatter());
        }

        private IEnumerator WaitAndBringUpNextBatter()
        {
            // Wait for any ongoing runner animations to complete
            Debug.Log($"[GameboardUI] WaitAndBringUpNextBatter: waiting for animations, isAnimatingRunners={isAnimatingRunners}");
            while (isAnimatingRunners)
            {
                yield return null;
            }
            Debug.Log("[GameboardUI] WaitAndBringUpNextBatter: animations complete, setting up new batter");
            
            // Now set up the new batter
            var batter = gameManager?.GetCurrentBatter();
            if (batter != null)
            {
                int lineupIndex = gameManager.GetCurrentBatterLineupIndex();
                bool isAway = gameManager.IsTopOfInning; // Away team bats in top of inning
                
                currentBatterInfo = new RunnerInfo
                {
                    PlayerName = batter.PlayerName,
                    Speed = batter.Speed,
                    LineupIndex = lineupIndex,
                    IsAwayTeam = isAway
                };
                
                Debug.Log($"[GameboardUI] WaitAndBringUpNextBatter: created currentBatterInfo for {batter.PlayerName}, lineupIndex={lineupIndex}, isAway={isAway}");
                
                // Mark this player as on the field
                if (isAway)
                    awayPlayersOnField.Add(lineupIndex);
                else
                    homePlayersOnField.Add(lineupIndex);
            }
            else
            {
                Debug.LogWarning("[GameboardUI] WaitAndBringUpNextBatter: batter is null!");
            }
            
            // Animate batter from lineup to home plate
            yield return StartCoroutine(AnimateBatterToPlate());
            Debug.Log($"[GameboardUI] WaitAndBringUpNextBatter: animation complete, currentBatterInfo.Card={currentBatterInfo?.Card != null}");
        }

        private IEnumerator AnimateBatterToPlate()
        {
            Debug.Log($"[GameboardUI] AnimateBatterToPlate: starting, currentBatterInfo={currentBatterInfo != null}");
            
            if (currentBatterInfo == null || gameManager == null)
            {
                Debug.LogWarning("[GameboardUI] AnimateBatterToPlate: currentBatterInfo or gameManager is null, exiting");
                yield break;
            }
            
            // Get the lineup card to animate from (using helper to account for UI swap)
            int lineupIndex = currentBatterInfo.LineupIndex;
            GameObject[] lineupCards = GetLineupCardsForTeam(currentBatterInfo.IsAwayTeam);
            
            Debug.Log($"[GameboardUI] AnimateBatterToPlate: lineupIndex={lineupIndex}, isAway={currentBatterInfo.IsAwayTeam}");
            
            if (lineupIndex < 0 || lineupIndex >= lineupCards.Length)
            {
                Debug.LogWarning($"[GameboardUI] AnimateBatterToPlate: lineupIndex {lineupIndex} out of range");
                yield break;
            }
            
            GameObject sourceCard = lineupCards[lineupIndex];
            if (sourceCard == null)
            {
                Debug.LogWarning("[GameboardUI] AnimateBatterToPlate: sourceCard is null");
                yield break;
            }
            
            // Small delay to ensure UI is ready after inning change
            yield return null;
            
            // Create the batter card on the field if needed
            if (batterCard == null)
            {
                Debug.Log("[GameboardUI] AnimateBatterToPlate: creating new batterCard");
                batterCard = CreateFieldCard(fieldArea, "BatterCard", homePos);
            }
            else
            {
                Debug.Log("[GameboardUI] AnimateBatterToPlate: reusing existing batterCard");
                // Reuse existing card but make sure it's active
                batterCard.SetActive(true);
            }
            
            // Get source position in field area coordinates
            RectTransform sourceRect = sourceCard.GetComponent<RectTransform>();
            RectTransform fieldRect = fieldArea.GetComponent<RectTransform>();
            
            // Convert source position to field area local position
            Vector2 sourceWorldPos = sourceRect.position;
            Vector2 startPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                fieldRect, sourceWorldPos, null, out startPos);
            
            // Hide the lineup card
            sourceCard.SetActive(false);
            
            // Position batter card at start and show it
            var batter = gameManager.GetCurrentBatter();
            if (batter != null)
            {
                UpdateBatterCardDisplay(batterCard, batter);
            }
            batterCard.SetActive(true);
            
            // Assign card to batter info BEFORE animation so it's available if at-bat ends quickly
            if (currentBatterInfo != null)
            {
                currentBatterInfo.Card = batterCard;
            }
            
            RectTransform batterRect = batterCard.GetComponent<RectTransform>();
            batterRect.anchoredPosition = startPos;
            
            // Animate to home plate
            yield return StartCoroutine(AnimateCardToPosition(batterCard, homePos));
            
            RefreshBoard();
        }

        private void OnAtBatEnded(AtBatOutcome outcome)
        {
            // Animate baserunner movement based on outcome
            StartCoroutine(AnimateAtBatResult(outcome));
        }

        private IEnumerator AnimateAtBatResult(AtBatOutcome outcome)
        {
            isAnimatingRunners = true;
            
            Debug.Log($"[GameboardUI] AnimateAtBatResult: outcome={outcome}, currentBatterInfo={currentBatterInfo != null}, batterCard={batterCard != null}, batterCard.Card={currentBatterInfo?.Card != null}");
            
            switch (outcome)
            {
                case AtBatOutcome.Walk:
                case AtBatOutcome.Single:
                    yield return StartCoroutine(AnimateSingleOrWalk());
                    break;
                case AtBatOutcome.Double:
                    yield return StartCoroutine(AnimateDouble());
                    break;
                case AtBatOutcome.Triple:
                    yield return StartCoroutine(AnimateTriple());
                    break;
                case AtBatOutcome.HomeRun:
                    yield return StartCoroutine(AnimateHomeRun());
                    break;
                case AtBatOutcome.Strikeout:
                case AtBatOutcome.Groundout:
                case AtBatOutcome.Flyout:
                    yield return StartCoroutine(AnimateBatterOut());
                    break;
                default:
                    // Unknown outcome - return batter to lineup
                    yield return StartCoroutine(AnimateBatterOut());
                    break;
            }
            
            isAnimatingRunners = false;
            RefreshBoard();
        }

        private IEnumerator AnimateBatterOut()
        {
            // Capture references immediately in case they get cleared during the delay
            RunnerInfo batterToReturn = currentBatterInfo;
            GameObject cardToUse = batterCard;
            
            // Batter is out - animate them back to their lineup position
            if (batterToReturn != null)
            {
                // Brief pause to show the out
                yield return new WaitForSeconds(0.2f);
                
                // If the card wasn't assigned yet (at-bat ended before animation completed),
                // use the batterCard directly
                if (batterToReturn.Card == null && cardToUse != null)
                {
                    batterToReturn.Card = cardToUse;
                }
                
                // Animate batter back to lineup
                if (batterToReturn.Card != null)
                {
                    yield return StartCoroutine(AnimateRunnerToLineup(batterToReturn));
                }
                else
                {
                    // Fallback: just return to lineup instantly
                    ReturnRunnerToLineup(batterToReturn);
                    if (cardToUse != null)
                    {
                        cardToUse.SetActive(false);
                    }
                }
                
                // Only clear if these are still the same references (not replaced by new batter)
                if (currentBatterInfo == batterToReturn)
                {
                    currentBatterInfo = null;
                }
                if (batterCard == cardToUse)
                {
                    batterCard = null;
                }
            }
        }

        private IEnumerator AnimateSingleOrWalk()
        {
            // Move all runners SIMULTANEOUSLY
            List<Coroutine> animations = new List<Coroutine>();
            
            // Runner on 3rd scores (runs home)
            RunnerInfo scoringRunner = runnerOnThird;
            if (scoringRunner != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerScoringFromThird(scoringRunner)));
            }
            
            // Runner on 2nd goes to 3rd
            RunnerInfo runnerToThird = runnerOnSecond;
            if (runnerToThird != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerToBase(runnerToThird, thirdPos)));
            }
            
            // Runner on 1st goes to 2nd
            RunnerInfo runnerToSecond = runnerOnFirst;
            if (runnerToSecond != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerToBase(runnerToSecond, secondPos)));
            }
            
            // Batter goes to first
            RunnerInfo batterToFirst = currentBatterInfo;
            GameObject batterCardRef = batterCard;
            
            // Ensure batter has card reference
            if (batterToFirst != null && batterToFirst.Card == null && batterCardRef != null)
            {
                batterToFirst.Card = batterCardRef;
            }
            
            if (batterToFirst != null && batterCardRef != null)
            {
                animations.Add(StartCoroutine(AnimateCardToPosition(batterCardRef, firstPos)));
            }
            
            // Wait for all animations to complete
            foreach (var anim in animations)
            {
                yield return anim;
            }
            
            // Update runner positions after all animations complete
            runnerOnThird = runnerToThird;
            runnerOnSecond = runnerToSecond;
            runnerOnFirst = batterToFirst;
            if (runnerOnFirst != null)
            {
                runnerOnFirst.Card = batterCardRef;
            }
            batterCard = null;
        }

        private IEnumerator AnimateDouble()
        {
            // Move all runners SIMULTANEOUSLY
            List<Coroutine> animations = new List<Coroutine>();
            
            // Runners on 2nd and 3rd score (each runs their path)
            RunnerInfo scoringRunner3 = runnerOnThird;
            RunnerInfo scoringRunner2 = runnerOnSecond;
            if (scoringRunner3 != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerScoringFromThird(scoringRunner3)));
            }
            if (scoringRunner2 != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerScoringFromSecond(scoringRunner2)));
            }
            
            // Runner on 1st goes to 3rd (via 2nd)
            RunnerInfo runnerToThird = runnerOnFirst;
            if (runnerToThird != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerViaBase(runnerToThird, secondPos, thirdPos)));
            }
            
            // Batter goes to second (via first)
            RunnerInfo batterToSecond = currentBatterInfo;
            GameObject batterCardRef = batterCard;
            
            // Ensure batter has card reference
            if (batterToSecond != null && batterToSecond.Card == null && batterCardRef != null)
            {
                batterToSecond.Card = batterCardRef;
            }
            
            if (batterToSecond != null && batterCardRef != null)
            {
                animations.Add(StartCoroutine(AnimateBatterViaBase(batterCardRef, firstPos, secondPos)));
            }
            
            // Wait for all animations to complete
            foreach (var anim in animations)
            {
                yield return anim;
            }
            
            // Update runner positions
            runnerOnThird = runnerToThird;
            runnerOnSecond = batterToSecond;
            if (runnerOnSecond != null)
            {
                runnerOnSecond.Card = batterCardRef;
            }
            runnerOnFirst = null;
            batterCard = null;
        }

        private IEnumerator AnimateTriple()
        {
            // Move all runners SIMULTANEOUSLY
            List<Coroutine> animations = new List<Coroutine>();
            
            // All existing runners score (each runs their path)
            RunnerInfo scoringRunner3 = runnerOnThird;
            RunnerInfo scoringRunner2 = runnerOnSecond;
            RunnerInfo scoringRunner1 = runnerOnFirst;
            
            if (scoringRunner3 != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerScoringFromThird(scoringRunner3)));
            }
            if (scoringRunner2 != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerScoringFromSecond(scoringRunner2)));
            }
            if (scoringRunner1 != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerScoringFromFirst(scoringRunner1)));
            }
            
            // Batter goes to third (via first, second)
            RunnerInfo batterToThird = currentBatterInfo;
            GameObject batterCardRef = batterCard;
            
            // Ensure batter has card reference
            if (batterToThird != null && batterToThird.Card == null && batterCardRef != null)
            {
                batterToThird.Card = batterCardRef;
            }
            
            if (batterToThird != null && batterCardRef != null)
            {
                animations.Add(StartCoroutine(AnimateBatterViaBases(batterCardRef, firstPos, secondPos, thirdPos)));
            }
            
            // Wait for all animations to complete
            foreach (var anim in animations)
            {
                yield return anim;
            }
            
            // Update runner positions
            runnerOnThird = batterToThird;
            if (runnerOnThird != null)
            {
                runnerOnThird.Card = batterCardRef;
            }
            runnerOnSecond = null;
            runnerOnFirst = null;
            batterCard = null;
        }

        private IEnumerator AnimateHomeRun()
        {
            // Move all runners SIMULTANEOUSLY
            List<Coroutine> animations = new List<Coroutine>();
            
            // All existing runners score (each runs their path)
            RunnerInfo scoringRunner3 = runnerOnThird;
            RunnerInfo scoringRunner2 = runnerOnSecond;
            RunnerInfo scoringRunner1 = runnerOnFirst;
            
            if (scoringRunner3 != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerScoringFromThird(scoringRunner3)));
            }
            if (scoringRunner2 != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerScoringFromSecond(scoringRunner2)));
            }
            if (scoringRunner1 != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerScoringFromFirst(scoringRunner1)));
            }
            
            // Batter runs all bases and scores
            RunnerInfo batterScoring = currentBatterInfo;
            GameObject batterCardRef = batterCard;
            
            // Ensure batter has card reference
            if (batterScoring != null && batterScoring.Card == null && batterCardRef != null)
            {
                batterScoring.Card = batterCardRef;
            }
            
            if (batterScoring != null && batterCardRef != null)
            {
                animations.Add(StartCoroutine(AnimateBatterHomeRun(batterScoring, batterCardRef)));
            }
            
            // Wait for all animations to complete
            foreach (var anim in animations)
            {
                yield return anim;
            }
            
            // Clear all runners
            runnerOnThird = null;
            runnerOnSecond = null;
            runnerOnFirst = null;
            currentBatterInfo = null;
            batterCard = null;
        }

        private IEnumerator AnimateRunnerViaBase(RunnerInfo runner, Vector2 viaPos, Vector2 endPos)
        {
            if (runner?.Card == null) yield break;
            
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, viaPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, endPos));
        }

        private IEnumerator AnimateBatterViaBase(GameObject card, Vector2 viaPos, Vector2 endPos)
        {
            if (card == null) yield break;
            
            yield return StartCoroutine(AnimateCardToPosition(card, viaPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(card, endPos));
        }

        private IEnumerator AnimateBatterViaBases(GameObject card, Vector2 pos1, Vector2 pos2, Vector2 pos3)
        {
            if (card == null) yield break;
            
            yield return StartCoroutine(AnimateCardToPosition(card, pos1));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(card, pos2));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(card, pos3));
        }

        private IEnumerator AnimateBatterHomeRun(RunnerInfo batter, GameObject card)
        {
            if (card == null) yield break;
            
            yield return StartCoroutine(AnimateCardToPosition(card, firstPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(card, secondPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(card, thirdPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(card, homePos));
            yield return new WaitForSeconds(0.15f);
            
            // Return batter to lineup after scoring
            yield return StartCoroutine(AnimateRunnerToLineup(batter));
        }

        private IEnumerator AnimateRunnerToBase(RunnerInfo runner, Vector2 targetPos)
        {
            if (runner?.Card == null) yield break;
            
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, targetPos));
        }

        private IEnumerator AnimateRunnerScoringFromThird(RunnerInfo runner)
        {
            if (runner?.Card == null) yield break;
            
            // Runner on 3rd just goes home
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, homePos));
            yield return new WaitForSeconds(0.15f);
            yield return StartCoroutine(AnimateRunnerToLineup(runner));
        }

        private IEnumerator AnimateRunnerScoringFromSecond(RunnerInfo runner)
        {
            if (runner?.Card == null) yield break;
            
            // Runner on 2nd goes to 3rd, then home
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, thirdPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, homePos));
            yield return new WaitForSeconds(0.15f);
            yield return StartCoroutine(AnimateRunnerToLineup(runner));
        }

        private IEnumerator AnimateRunnerScoringFromFirst(RunnerInfo runner)
        {
            if (runner?.Card == null) yield break;
            
            // Runner on 1st goes to 2nd, 3rd, then home
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, secondPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, thirdPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, homePos));
            yield return new WaitForSeconds(0.15f);
            yield return StartCoroutine(AnimateRunnerToLineup(runner));
        }

        private IEnumerator AnimateRunnerScoring(RunnerInfo runner)
        {
            // Legacy method - just go to home (used when we don't know the base)
            if (runner?.Card == null) yield break;
            
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, homePos));
            yield return new WaitForSeconds(0.15f);
            yield return StartCoroutine(AnimateRunnerToLineup(runner));
        }

        private IEnumerator AnimateCardToPosition(GameObject card, Vector2 targetPos)
        {
            if (card == null) yield break;
            
            RectTransform rect = card.GetComponent<RectTransform>();
            if (rect == null) yield break;
            
            Vector2 startPos = rect.anchoredPosition;
            float duration = 0.4f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }
            
            rect.anchoredPosition = targetPos;
        }

        private void OnScoreChanged(int home, int away)
        {
            // Score display is handled elsewhere, but refresh board
            if (!isAnimatingRunners)
                RefreshBoard();
        }

        private void OnInningChanged(int inning, bool isTop)
        {
            // Animate all runners back to lineup before clearing
            StartCoroutine(AnimateInningChange());
        }

        private IEnumerator AnimateInningChange()
        {
            isAnimatingRunners = true;
            
            // Capture the current state BEFORE any new batter is set up
            // This prevents race conditions with the new batter's OnAtBatStarted
            RunnerInfo savedRunner3 = runnerOnThird;
            RunnerInfo savedRunner2 = runnerOnSecond;
            RunnerInfo savedRunner1 = runnerOnFirst;
            RunnerInfo savedBatter = currentBatterInfo;
            GameObject savedBatterCard = batterCard;
            
            // Clear references immediately so new batter can be set up
            runnerOnFirst = null;
            runnerOnSecond = null;
            runnerOnThird = null;
            currentBatterInfo = null;
            batterCard = null;
            
            // Collect all runners that need to return to lineup
            List<RunnerInfo> runnersToReturn = new List<RunnerInfo>();
            if (savedRunner3 != null) runnersToReturn.Add(savedRunner3);
            if (savedRunner2 != null) runnersToReturn.Add(savedRunner2);
            if (savedRunner1 != null) runnersToReturn.Add(savedRunner1);
            if (savedBatter != null) runnersToReturn.Add(savedBatter);
            
            // Start all return animations simultaneously
            List<Coroutine> animations = new List<Coroutine>();
            foreach (var runner in runnersToReturn)
            {
                if (runner?.Card != null)
                {
                    animations.Add(StartCoroutine(AnimateRunnerToLineup(runner)));
                }
            }
            
            // Wait for all animations to complete
            foreach (var anim in animations)
            {
                yield return anim;
            }
            
            // Clear tracking sets
            awayPlayersOnField.Clear();
            homePlayersOnField.Clear();
            
            // Hide the saved field cards (not the new ones that might have been created)
            if (savedBatterCard != null) savedBatterCard.SetActive(false);
            if (firstBaseCard != null) firstBaseCard.SetActive(false);
            if (secondBaseCard != null) secondBaseCard.SetActive(false);
            if (thirdBaseCard != null) thirdBaseCard.SetActive(false);
            
            isAnimatingRunners = false;
            RefreshBoard();
        }

        private void ClearAllRunnersFromField()
        {
            // Instant version - just clean up tracking (used for immediate cleanup)
            ReturnRunnerToLineup(runnerOnFirst);
            ReturnRunnerToLineup(runnerOnSecond);
            ReturnRunnerToLineup(runnerOnThird);
            ReturnRunnerToLineup(currentBatterInfo);
            
            runnerOnFirst = null;
            runnerOnSecond = null;
            runnerOnThird = null;
            currentBatterInfo = null;
            
            // Clear tracking sets
            awayPlayersOnField.Clear();
            homePlayersOnField.Clear();
            
            // Hide field cards
            if (batterCard != null) batterCard.SetActive(false);
            if (firstBaseCard != null) firstBaseCard.SetActive(false);
            if (secondBaseCard != null) secondBaseCard.SetActive(false);
            if (thirdBaseCard != null) thirdBaseCard.SetActive(false);
        }

        private void ReturnRunnerToLineup(RunnerInfo runner)
        {
            // Instant version - just clean up tracking (used for inning changes)
            if (runner == null) return;
            
            // Remove from field tracking
            if (runner.IsAwayTeam)
                awayPlayersOnField.Remove(runner.LineupIndex);
            else
                homePlayersOnField.Remove(runner.LineupIndex);
            
            // Show the lineup card again (using helper to account for UI swap)
            GameObject[] lineupCards = GetLineupCardsForTeam(runner.IsAwayTeam);
            if (runner.LineupIndex >= 0 && runner.LineupIndex < lineupCards.Length)
            {
                if (lineupCards[runner.LineupIndex] != null)
                    lineupCards[runner.LineupIndex].SetActive(true);
            }
            
            // Hide the field card
            if (runner.Card != null)
                runner.Card.SetActive(false);
        }

        private IEnumerator AnimateRunnerToLineup(RunnerInfo runner)
        {
            // Animated version - moves card back to lineup position
            if (runner == null || runner.Card == null) yield break;
            
            // Get the lineup card position (using helper to account for UI swap)
            GameObject[] lineupCards = GetLineupCardsForTeam(runner.IsAwayTeam);
            if (runner.LineupIndex < 0 || runner.LineupIndex >= lineupCards.Length) yield break;
            
            GameObject targetLineupCard = lineupCards[runner.LineupIndex];
            if (targetLineupCard == null) yield break;
            
            // Get target position in field area coordinates
            RectTransform targetRect = targetLineupCard.GetComponent<RectTransform>();
            RectTransform fieldRect = fieldArea.GetComponent<RectTransform>();
            
            Vector2 targetWorldPos = targetRect.position;
            Vector2 targetPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                fieldRect, targetWorldPos, null, out targetPos);
            
            // Animate the card to the lineup position
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, targetPos));
            
            // Now do the cleanup
            ReturnRunnerToLineup(runner);
        }

        private void OnGameMessage(string message)
        {
            if (gameMessageText != null)
            {
                gameMessageText.text = message;
            }
        }

        private void OnGameStateChanged(GameState newState)
        {
            Debug.Log($"[GameboardUI] OnGameStateChanged received - newState={newState}");
            RefreshBoard();
            
            // On clients, trigger at-bat setup when transitioning to DefenseTurn
            // (This handles the case where OnAtBatStarted doesn't fire on clients)
            if (newState == GameState.DefenseTurn && currentBatterInfo == null)
            {
                Debug.Log("[GameboardUI] OnGameStateChanged: DefenseTurn with no batter - setting up batter");
                StartCoroutine(WaitAndBringUpNextBatter());
            }
        }
        
        private void OnDiceRollStarted()
        {
            Debug.Log("[GameboardUI] Dice roll started");
            // The 3D dice roller handles its own display
        }
        
        private void OnDiceRollComplete(int result)
        {
            Debug.Log($"[GameboardUI] 3D Dice animation complete - result: {result}");
            // The game manager's dice roller handles the actual game logic
            // This is just for the visual 3D dice animation
        }
        
        private IEnumerator AnimateDiceRoll()
        {
            if (diceResultText == null) yield break;
            
            // Show random numbers while rolling
            float rollDuration = 1.5f;
            float elapsed = 0f;
            float interval = 0.05f;
            
            while (elapsed < rollDuration)
            {
                int randomNum = UnityEngine.Random.Range(1, 21);
                diceResultText.text = randomNum.ToString();
                diceResultText.color = Color.white;
                
                yield return new WaitForSeconds(interval);
                elapsed += interval;
                
                // Slow down towards the end
                if (elapsed > rollDuration * 0.7f)
                    interval = 0.1f;
            }
        }
        
        private IEnumerator HideDiceDisplayAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (diceDisplayPanel != null)
            {
                diceDisplayPanel.SetActive(false);
            }
        }

        #endregion

        #region Board Updates

        public void RefreshBoard()
        {
            if (gameManager == null) return;
            
            UpdateScorebug();
            UpdateLineups();
            UpdatePitcher();
            UpdateBatter();
            UpdateBaserunners();
            UpdateTurnIndicator();
            UpdateOutcomeCharts();
        }

        private void UpdateScorebug()
        {
            if (gameManager == null) return;

            // Update scores
            if (awayScoreText != null)
                awayScoreText.text = gameManager.AwayScore.ToString();
            if (homeScoreText != null)
                homeScoreText.text = gameManager.HomeScore.ToString();

            // Update inning
            if (inningText != null)
            {
                string arrow = gameManager.IsTopOfInning ? "▲" : "▼";
                inningText.text = $"{arrow} {gameManager.CurrentInning}";
            }

            // Update outs
            int outs = gameManager.Outs;
            if (outsText != null)
                outsText.text = $"{outs} OUT{(outs != 1 ? "S" : "")}";

            // Update out indicators
            if (outIndicators != null)
            {
                for (int i = 0; i < outIndicators.Length; i++)
                {
                    if (outIndicators[i] != null)
                    {
                        outIndicators[i].color = i < outs ? 
                            new Color(1f, 0.3f, 0.3f, 1f) : // Red for recorded outs
                            new Color(0.3f, 0.3f, 0.3f, 1f); // Gray for available
                    }
                }
            }
        }

        private void UpdateTurnIndicator()
        {
            if (gameManager == null || turnIndicatorText == null) return;

            // Use IsLocalPlayerTurn which handles both CPU and multiplayer modes
            bool isPlayerTurn = gameManager.IsLocalPlayerTurn();
            string turnText = "";
            
            // Determine opponent label based on game mode
            string opponentLabel = gameManager.IsCPUGame ? "CPU" : "OPPONENT";

            if (gameManager.CurrentState == GameState.DefenseTurn)
            {
                // Defense (pitcher) rolls
                if (isPlayerTurn)
                {
                    turnText = "YOUR PITCH - ROLL!";
                }
                else
                {
                    turnText = $"{opponentLabel} PITCHING...";
                }
            }
            else if (gameManager.CurrentState == GameState.OffenseTurn)
            {
                // Offense (batter) rolls
                if (isPlayerTurn)
                {
                    turnText = "YOUR AT-BAT - ROLL!";
                }
                else
                {
                    turnText = $"{opponentLabel} BATTING...";
                }
            }
            else if (gameManager.CurrentState == GameState.OptionalAction)
            {
                if (isPlayerTurn)
                {
                    turnText = "YOUR DECISION...";
                }
                else
                {
                    turnText = $"{opponentLabel} DECIDING...";
                }
            }
            else if (gameManager.CurrentState == GameState.AtBatAction || 
                     gameManager.CurrentState == GameState.UpdateBaseRunners ||
                     gameManager.CurrentState == GameState.NextBatterUp)
            {
                turnText = "RESOLVING...";
            }
            else if (gameManager.CurrentState == GameState.WaitingForPlayers)
            {
                turnText = "WAITING FOR PLAYERS...";
            }
            else
            {
                turnText = "GAME IN PROGRESS";
            }

            turnIndicatorText.text = turnText;
            turnIndicatorText.color = isPlayerTurn ? Color.yellow : Color.white;

            // Enable/disable roll button based on turn
            if (rollDiceButton != null)
            {
                rollDiceButton.interactable = isPlayerTurn;
            }
            
        }

        private void UpdateOutcomeCharts()
        {
            if (gameManager == null) return;

            var pitcher = gameManager.GetCurrentPitcher();
            var batter = gameManager.GetCurrentBatter();
            bool batterHasAdvantage = gameManager.BatterHasAdvantage;

            // Update pitcher chart
            if (pitcherChartPanel != null && pitcher != null)
            {
                var pitcherBg = pitcherChartPanel.GetComponent<Image>();
                if (pitcherBg != null)
                {
                    pitcherBg.color = !batterHasAdvantage ? advantageColor : noAdvantageColor;
                }
                
                if (pitcherChartText != null && pitcher.OutcomeCard != null)
                {
                    pitcherChartText.text = FormatOutcomeChart(pitcher.OutcomeCard, pitcher.PlayerName, !batterHasAdvantage);
                }
            }

            // Update batter chart
            if (batterChartPanel != null && batter != null)
            {
                var batterBg = batterChartPanel.GetComponent<Image>();
                if (batterBg != null)
                {
                    batterBg.color = batterHasAdvantage ? advantageColor : noAdvantageColor;
                }
                
                if (batterChartText != null && batter.OutcomeCard != null)
                {
                    batterChartText.text = FormatOutcomeChart(batter.OutcomeCard, batter.PlayerName, batterHasAdvantage);
                }
            }
        }

        private string FormatOutcomeChart(OutcomeCard chart, string playerName, bool hasAdvantage)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{playerName}</b>");
            if (hasAdvantage)
                sb.AppendLine("<color=#90EE90>★ ADVANTAGE ★</color>\n");
            else
                sb.AppendLine("");
            
            if (chart.Strikeout != null)
                sb.AppendLine($"K:  {FormatRange(chart.Strikeout)}");
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
            return $"{range.MinRoll}-{range.MaxRoll}";
        }

        private void UpdateLineups()
        {
            if (gameManager == null) return;

            // User's team is always on bottom, opponent on top.
            // So: bottom lineup (homeLineupCards) shows user's team
            //     top lineup (awayLineupCards) shows opponent's team
            
            bool userIsAway = IsLocalPlayerAway();
            
            // Get batters for each UI position
            var topBatters = userIsAway ? gameManager.GetHomeBatters() : gameManager.GetAwayBatters();
            var bottomBatters = userIsAway ? gameManager.GetAwayBatters() : gameManager.GetHomeBatters();
            var topPlayersOnField = userIsAway ? homePlayersOnField : awayPlayersOnField;
            var bottomPlayersOnField = userIsAway ? awayPlayersOnField : homePlayersOnField;
            bool topTeamBatting = userIsAway ? !gameManager.IsTopOfInning : gameManager.IsTopOfInning;
            bool bottomTeamBatting = userIsAway ? gameManager.IsTopOfInning : !gameManager.IsTopOfInning;

            // Update top lineup cards (opponent)
            if (topBatters != null)
            {
                for (int i = 0; i < 9 && i < topBatters.Length; i++)
                {
                    if (awayLineupCards[i] != null && topBatters[i] != null)
                    {
                        // Hide card if this player is on the field (batting or on base)
                        bool isOnField = topPlayersOnField.Contains(i);
                        
                        // Also check if this is the current batter (they should be hidden)
                        bool isCurrentBatterOnField = topTeamBatting && 
                            i == gameManager.GetCurrentBatterLineupIndex() &&
                            currentBatterInfo != null;
                        
                        awayLineupCards[i].SetActive(!isOnField && !isCurrentBatterOnField);
                        
                        if (!isOnField && !isCurrentBatterOnField)
                        {
                            UpdateCardDisplay(awayLineupCards[i], topBatters[i]);
                            
                            // No highlighting needed - current batter is on field
                            HighlightCard(awayLineupCards[i], false);
                        }
                    }
                }
            }

            // Update bottom lineup cards (user's team)
            if (bottomBatters != null)
            {
                for (int i = 0; i < 9 && i < bottomBatters.Length; i++)
                {
                    if (homeLineupCards[i] != null && bottomBatters[i] != null)
                    {
                        // Hide card if this player is on the field (batting or on base)
                        bool isOnField = bottomPlayersOnField.Contains(i);
                        
                        // Also check if this is the current batter (they should be hidden)
                        bool isCurrentBatterOnField = bottomTeamBatting && 
                            i == gameManager.GetCurrentBatterLineupIndex() &&
                            currentBatterInfo != null;
                        
                        homeLineupCards[i].SetActive(!isOnField && !isCurrentBatterOnField);
                        
                        if (!isOnField && !isCurrentBatterOnField)
                        {
                            UpdateCardDisplay(homeLineupCards[i], bottomBatters[i]);
                            
                            // No highlighting needed - current batter is on field
                            HighlightCard(homeLineupCards[i], false);
                        }
                    }
                }
            }
        }

        private void UpdatePitcher()
        {
            if (gameManager == null) return;
            
            // Don't update while animating
            if (isAnimatingPitcher) return;

            var pitcher = gameManager.GetCurrentPitcher();
            if (pitcher == null) return;

            // Determine which team is currently pitching
            // Top of inning = home team pitches, Bottom of inning = away team pitches
            bool homePitching = gameManager.IsTopOfInning;
            
            // Check if we need to animate a pitcher change
            if (!pitcherOnMound || currentPitcherIsHome != homePitching)
            {
                // Pitcher changed - animate the transition
                // Don't update lineup cards here - let the animation handle visibility
                StartCoroutine(AnimatePitcherChange(homePitching));
                return; // Exit early - animation will handle everything
            }
            
            // Just update the display if pitcher is already on mound
            if (pitcherOnMoundCard != null)
            {
                UpdatePitcherCardDisplay(pitcherOnMoundCard, pitcher);
            }
            
            // Update the non-pitching team's pitcher card in lineup (always visible)
            bool userIsAway = IsLocalPlayerAway();
            var userPitcher = userIsAway ? gameManager.GetAwayPitcher() : gameManager.GetHomePitcher();
            var opponentPitcher = userIsAway ? gameManager.GetHomePitcher() : gameManager.GetAwayPitcher();
            bool opponentPitching = userIsAway ? homePitching : !homePitching;
            
            GameObject topPitcherCard = awayPitcherCard;
            GameObject bottomPitcherCard = homePitcherCard;
            
            // Update and show the pitcher that's NOT on the mound
            if (userPitcher != null)
            {
                UpdatePitcherCardDisplay(bottomPitcherCard, userPitcher);
                bottomPitcherCard.SetActive(opponentPitching); // Show if opponent is pitching
            }
            if (opponentPitcher != null)
            {
                UpdatePitcherCardDisplay(topPitcherCard, opponentPitcher);
                topPitcherCard.SetActive(!opponentPitching); // Show if user is pitching
            }
        }

        private IEnumerator AnimatePitcherChange(bool newPitcherIsHome)
        {
            isAnimatingPitcher = true;
            
            // If there's a pitcher on mound, animate them back to lineup first
            if (pitcherOnMound && pitcherOnMoundCard != null)
            {
                yield return StartCoroutine(AnimatePitcherToLineup(!newPitcherIsHome));
            }
            
            // Now animate the new pitcher to the mound
            yield return StartCoroutine(AnimatePitcherToMound(newPitcherIsHome));
            
            pitcherOnMound = true;
            currentPitcherIsHome = newPitcherIsHome;
            
            // After animation, update and show the non-pitching team's card
            bool userIsAway = IsLocalPlayerAway();
            var userPitcher = userIsAway ? gameManager.GetAwayPitcher() : gameManager.GetHomePitcher();
            var opponentPitcher = userIsAway ? gameManager.GetHomePitcher() : gameManager.GetAwayPitcher();
            bool opponentPitching = userIsAway ? newPitcherIsHome : !newPitcherIsHome;
            
            // Show the pitcher that's NOT on the mound
            if (userPitcher != null)
            {
                UpdatePitcherCardDisplay(homePitcherCard, userPitcher);
                homePitcherCard.SetActive(opponentPitching);
            }
            if (opponentPitcher != null)
            {
                UpdatePitcherCardDisplay(awayPitcherCard, opponentPitcher);
                awayPitcherCard.SetActive(!opponentPitching);
            }
            
            isAnimatingPitcher = false;
        }

        private IEnumerator AnimatePitcherToMound(bool isHomePitcher)
        {
            var pitcher = isHomePitcher ? gameManager.GetHomePitcher() : gameManager.GetAwayPitcher();
            if (pitcher == null) yield break;
            
            // Get the source pitcher card from lineup for the pitching team
            GameObject sourcePitcherCard = GetPitcherCardForTeam(isHomePitcher);
            
            // Create pitcher card on mound if it doesn't exist
            Vector2 moundPos = pitcherMoundPosition.anchoredPosition + new Vector2(0, 40);
            if (pitcherOnMoundCard == null)
            {
                pitcherOnMoundCard = CreateFieldCard(fieldArea, "PitcherOnMound", moundPos);
            }
            
            // Get source position in field area coordinates
            RectTransform sourceRect = sourcePitcherCard.GetComponent<RectTransform>();
            RectTransform fieldRect = fieldArea.GetComponent<RectTransform>();
            
            Vector2 sourceWorldPos = sourceRect.position;
            Vector2 startPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                fieldRect, sourceWorldPos, null, out startPos);
            
            // Hide the lineup card
            sourcePitcherCard.SetActive(false);
            
            // Position mound card at start and show it
            UpdatePitcherCardDisplay(pitcherOnMoundCard, pitcher);
            pitcherOnMoundCard.SetActive(true);
            
            RectTransform moundRect = pitcherOnMoundCard.GetComponent<RectTransform>();
            moundRect.anchoredPosition = startPos;
            
            // Animate to mound
            yield return StartCoroutine(AnimateCardToPosition(pitcherOnMoundCard, moundPos));
        }

        private IEnumerator AnimatePitcherToLineup(bool isHomePitcher)
        {
            if (pitcherOnMoundCard == null) yield break;
            
            // Get the target pitcher card in lineup
            GameObject targetPitcherCard = GetPitcherCardForTeam(isHomePitcher);
            
            // Get target position in field area coordinates
            RectTransform targetRect = targetPitcherCard.GetComponent<RectTransform>();
            RectTransform fieldRect = fieldArea.GetComponent<RectTransform>();
            
            Vector2 targetWorldPos = targetRect.position;
            Vector2 targetPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                fieldRect, targetWorldPos, null, out targetPos);
            
            // Animate to lineup position
            yield return StartCoroutine(AnimateCardToPosition(pitcherOnMoundCard, targetPos));
            
            // Hide mound card and show lineup card
            pitcherOnMoundCard.SetActive(false);
            targetPitcherCard.SetActive(true);
        }

        private void UpdateBatter()
        {
            if (gameManager == null || isAnimatingRunners) return;

            // If we have a current batter info with a card, it's already on the field
            // Don't recreate - the animation handles positioning
            if (currentBatterInfo != null && currentBatterInfo.Card != null)
            {
                // Card already exists and is being managed by animation system
                return;
            }

            var batter = gameManager.GetCurrentBatter();
            if (batter == null) return;

            // Only create batter card if we don't have one yet
            // (This is a fallback - normally AnimateBatterToPlate handles this)
            if (batterCard == null)
            {
                batterCard = CreateFieldCard(fieldArea, "BatterCard", homePos);
                batterCard.SetActive(true);
                UpdateBatterCardDisplay(batterCard, batter);
            }
        }

        private void UpdateBaserunners()
        {
            if (gameManager == null || isAnimatingRunners) return;

            // Runners use their own cards (the same card that was the batter)
            // We just need to ensure they're visible and positioned correctly
            
            // First base - use runner's own card
            if (runnerOnFirst != null && runnerOnFirst.Card != null)
            {
                runnerOnFirst.Card.SetActive(true);
                UpdateRunnerCardDisplay(runnerOnFirst.Card, runnerOnFirst);
            }

            // Second base - use runner's own card
            if (runnerOnSecond != null && runnerOnSecond.Card != null)
            {
                runnerOnSecond.Card.SetActive(true);
                UpdateRunnerCardDisplay(runnerOnSecond.Card, runnerOnSecond);
            }

            // Third base - use runner's own card
            if (runnerOnThird != null && runnerOnThird.Card != null)
            {
                runnerOnThird.Card.SetActive(true);
                UpdateRunnerCardDisplay(runnerOnThird.Card, runnerOnThird);
            }

            // Hide the old static base cards if they exist (legacy cleanup)
            if (firstBaseCard != null && (runnerOnFirst == null || runnerOnFirst.Card != firstBaseCard))
            {
                firstBaseCard.SetActive(false);
            }
            if (secondBaseCard != null && (runnerOnSecond == null || runnerOnSecond.Card != secondBaseCard))
            {
                secondBaseCard.SetActive(false);
            }
            if (thirdBaseCard != null && (runnerOnThird == null || runnerOnThird.Card != thirdBaseCard))
            {
                thirdBaseCard.SetActive(false);
            }
            else if (thirdBaseCard != null)
            {
                thirdBaseCard.SetActive(false);
            }
        }

        private void UpdateCardDisplay(GameObject cardObj, BatterCardData batter)
        {
            if (cardObj == null || batter == null) return;
            
            var text = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"{batter.PlayerName}\n{batter.Position}\nOB:{batter.OnBase}";
            }
        }

        private void UpdateBatterCardDisplay(GameObject cardObj, BatterCardData batter)
        {
            if (cardObj == null || batter == null) return;
            
            var text = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"<b>{batter.PlayerName}</b>\n{batter.Position}\nOB: {batter.OnBase}\nSPD: {batter.Speed}";
            }
        }

        private void UpdateRunnerCardDisplay(GameObject cardObj, RunnerInfo runner)
        {
            if (cardObj == null || runner == null) return;
            
            var text = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"<b>{runner.PlayerName}</b>\nSPD: {runner.Speed}";
            }
        }

        private void UpdatePitcherCardDisplay(GameObject cardObj, PitcherCardData pitcher)
        {
            if (cardObj == null || pitcher == null) return;
            
            var text = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"<b>{pitcher.PlayerName}</b>\nP\nCTRL: {pitcher.Control}";
            }
        }

        private void HighlightCard(GameObject cardObj, bool highlight)
        {
            if (cardObj == null) return;
            
            var img = cardObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = highlight ? activeCardColor : new Color(0.3f, 0.3f, 0.35f, 0.8f);
            }
        }

        private void UpdateCardPositions()
        {
            // Card movement is now handled by coroutines in AnimateAtBatResult
            // This method is kept for any future smooth position updates if needed
        }

        #endregion

        #region Public Methods

        public void Show()
        {
            if (gameboardPanel != null)
            {
                gameboardPanel.SetActive(true);
                RefreshBoard();
            }
        }

        public void Hide()
        {
            if (gameboardPanel != null)
            {
                gameboardPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Animate a card moving from one base to another
        /// </summary>
        public void AnimateCardToBase(int fromBase, int toBase)
        {
            // fromBase: 0=home, 1=first, 2=second, 3=third
            // toBase: same encoding, 4=scored
            
            // This will be called by the game manager when baserunners advance
            RefreshBoard();
        }

        #endregion
    }
}
