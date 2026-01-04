using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MLBShowdown.Core;
using MLBShowdown.Network;

namespace MLBShowdown.UI
{
    public class GameOverUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TextMeshProUGUI winnerText;
        [SerializeField] private TextMeshProUGUI finalScoreText;
        [SerializeField] private TextMeshProUGUI mvpText;

        [Header("Buttons")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button viewStatsButton;

        [Header("Stats Panel")]
        [SerializeField] private GameObject statsPanel;
        [SerializeField] private BoxScoreUI boxScoreUI;

        private NetworkGameManager gameManager;

        void Start()
        {
            if (gameOverPanel == null)
            {
                CreateGameOverPanel();
            }

            SetupButtons();
            Hide();
        }

        void Update()
        {
            if (gameManager == null)
            {
                gameManager = NetworkGameManager.Instance;
                if (gameManager != null)
                {
                    gameManager.OnGameStateChanged += HandleStateChanged;
                }
            }
        }

        void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged -= HandleStateChanged;
            }
        }

        private void CreateGameOverPanel()
        {
            gameOverPanel = new GameObject("GameOverPanel");
            gameOverPanel.transform.SetParent(transform);

            RectTransform rect = gameOverPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Darkened background
            var bg = gameOverPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.85f);

            // Content container
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(gameOverPanel.transform);

            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.2f, 0.2f);
            contentRect.anchorMax = new Vector2(0.8f, 0.8f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var contentBg = contentObj.AddComponent<Image>();
            contentBg.color = new Color(0.15f, 0.15f, 0.2f, 0.98f);

            // Winner text
            winnerText = CreateText(contentObj.transform, "WinnerText", "GAME OVER", 
                new Vector2(0.5f, 0.85f), 48, FontStyles.Bold);

            // Final score
            finalScoreText = CreateText(contentObj.transform, "FinalScore", "HOME 0 - AWAY 0", 
                new Vector2(0.5f, 0.7f), 36);

            // MVP
            mvpText = CreateText(contentObj.transform, "MVP", "MVP: Player Name", 
                new Vector2(0.5f, 0.55f), 24);

            // Buttons
            playAgainButton = CreateButton(contentObj.transform, "PlayAgain", "PLAY AGAIN", 
                new Vector2(0.3f, 0.25f));
            mainMenuButton = CreateButton(contentObj.transform, "MainMenu", "MAIN MENU", 
                new Vector2(0.7f, 0.25f));
            viewStatsButton = CreateButton(contentObj.transform, "ViewStats", "VIEW STATS", 
                new Vector2(0.5f, 0.4f));
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text, 
            Vector2 anchor, int fontSize, FontStyles style = FontStyles.Normal)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(500, 60);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return tmp;
        }

        private Button CreateButton(Transform parent, string name, string text, Vector2 anchor)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(180, 50);

            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.4f, 0.6f);

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return button;
        }

        private void SetupButtons()
        {
            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(OnPlayAgainClicked);

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);

            if (viewStatsButton != null)
                viewStatsButton.onClick.AddListener(OnViewStatsClicked);
        }

        private void HandleStateChanged(GameState newState)
        {
            if (newState == GameState.GameOver)
            {
                ShowGameOver();
            }
        }

        public void ShowGameOver()
        {
            if (gameManager == null) return;

            int homeScore = gameManager.HomeScore;
            int awayScore = gameManager.AwayScore;

            // Determine winner
            string winner;
            if (homeScore > awayScore)
            {
                winner = "HOME TEAM WINS!";
                if (winnerText != null) winnerText.color = new Color(0.3f, 0.8f, 0.3f);
            }
            else if (awayScore > homeScore)
            {
                winner = "AWAY TEAM WINS!";
                if (winnerText != null) winnerText.color = new Color(0.8f, 0.3f, 0.3f);
            }
            else
            {
                winner = "TIE GAME!";
                if (winnerText != null) winnerText.color = Color.yellow;
            }

            if (winnerText != null)
                winnerText.text = winner;

            if (finalScoreText != null)
                finalScoreText.text = $"FINAL: HOME {homeScore} - AWAY {awayScore}";

            // Determine MVP (simplified - just pick a player with most hits or HRs)
            if (mvpText != null)
            {
                var mvpBatter = FindMVP();
                if (mvpBatter != null)
                {
                    mvpText.text = $"MVP: {mvpBatter.PlayerName} ({mvpBatter.Hits} H, {mvpBatter.HomeRuns} HR, {mvpBatter.RBIs} RBI)";
                }
                else
                {
                    mvpText.text = "";
                }
            }

            Show();
        }

        private Cards.BatterCardData FindMVP()
        {
            if (gameManager == null) return null;

            Cards.BatterCardData mvp = null;
            int bestScore = -1;

            // Check all batters from both teams
            for (int i = 0; i < 9; i++)
            {
                var homeBatter = gameManager.GetBatter(gameManager.HomeBatterIndices.Get(i));
                var awayBatter = gameManager.GetBatter(gameManager.AwayBatterIndices.Get(i));

                if (homeBatter != null)
                {
                    int score = CalculateMVPScore(homeBatter);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        mvp = homeBatter;
                    }
                }

                if (awayBatter != null)
                {
                    int score = CalculateMVPScore(awayBatter);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        mvp = awayBatter;
                    }
                }
            }

            return mvp;
        }

        private int CalculateMVPScore(Cards.BatterCardData batter)
        {
            // Simple MVP calculation: HR worth 4, RBI worth 2, Hits worth 1, Runs worth 1
            return (batter.HomeRuns * 4) + (batter.RBIs * 2) + batter.Hits + batter.Runs;
        }

        private void OnPlayAgainClicked()
        {
            Hide();
            
            // Restart game
            if (gameManager != null)
            {
                gameManager.RPC_StartGame(gameManager.IsCPUGame, gameManager.CPUIsHome);
            }
        }

        private void OnMainMenuClicked()
        {
            Hide();
            
            // Disconnect and show main menu
            if (NetworkRunnerHandler.Instance != null)
            {
                NetworkRunnerHandler.Instance.Disconnect();
            }

            // Find and show main menu
            var mainMenu = FindObjectOfType<MainMenuUI>();
            if (mainMenu != null)
            {
                mainMenu.gameObject.SetActive(true);
            }
        }

        private void OnViewStatsClicked()
        {
            if (boxScoreUI != null)
            {
                boxScoreUI.Toggle();
            }
            else if (statsPanel != null)
            {
                statsPanel.SetActive(!statsPanel.activeSelf);
            }
        }

        public void Show()
        {
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);
        }

        public void Hide()
        {
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
            
            if (statsPanel != null)
                statsPanel.SetActive(false);
        }
    }
}
