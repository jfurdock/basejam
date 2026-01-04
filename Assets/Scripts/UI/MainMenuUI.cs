using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MLBShowdown.Network;

namespace MLBShowdown.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject connectingPanel;
        [SerializeField] private GameObject onlineMenuPanel;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button playOnlineButton;
        [SerializeField] private Button playCPUButton;
        [SerializeField] private Button quitButton;

        [Header("Online Menu")]
        [SerializeField] private Button hostGameButton;
        [SerializeField] private Button joinGameButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Lobby")]
        [SerializeField] private TextMeshProUGUI lobbyStatusText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveLobbyButton;

        private NetworkRunnerHandler networkHandler;
        private bool isHost;

        void Start()
        {
            // Create UI elements if not assigned (for programmatic creation)
            if (mainMenuPanel == null)
            {
                CreateUIElements();
            }
            SetupButtons();
            ShowMainMenu();
        }

        private void CreateUIElements()
        {
            // Main Menu Panel
            mainMenuPanel = CreatePanel("MainMenuPanel");
            
            // Title
            CreateText(mainMenuPanel.transform, "MLB Showdown", 48, new Vector2(0, 150), TextAlignmentOptions.Center);
            
            // Play vs CPU Button
            playCPUButton = CreateButton(mainMenuPanel.transform, "Play vs CPU", new Vector2(0, 50));
            
            // Play Online Button
            playOnlineButton = CreateButton(mainMenuPanel.transform, "Play Online", new Vector2(0, -20));
            
            // Quit Button
            quitButton = CreateButton(mainMenuPanel.transform, "Quit", new Vector2(0, -90));

            // Connecting Panel
            connectingPanel = CreatePanel("ConnectingPanel");
            statusText = CreateText(connectingPanel.transform, "Connecting...", 24, Vector2.zero, TextAlignmentOptions.Center);
            connectingPanel.SetActive(false);

            // Lobby Panel
            lobbyPanel = CreatePanel("LobbyPanel");
            lobbyStatusText = CreateText(lobbyPanel.transform, "Lobby", 24, new Vector2(0, 100), TextAlignmentOptions.Center);
            playerCountText = CreateText(lobbyPanel.transform, "Players: 0/2", 20, new Vector2(0, 50), TextAlignmentOptions.Center);
            startGameButton = CreateButton(lobbyPanel.transform, "Start Game", new Vector2(0, -20));
            leaveLobbyButton = CreateButton(lobbyPanel.transform, "Leave", new Vector2(0, -90));
            lobbyPanel.SetActive(false);

            // Online Menu Panel (Host/Join options)
            onlineMenuPanel = CreatePanel("OnlineMenuPanel");
            CreateText(onlineMenuPanel.transform, "Online Play", 36, new Vector2(0, 150), TextAlignmentOptions.Center);
            
            // Room name input
            GameObject inputObj = new GameObject("RoomNameInput");
            inputObj.transform.SetParent(onlineMenuPanel.transform);
            RectTransform inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.anchoredPosition = new Vector2(0, 70);
            inputRect.sizeDelta = new Vector2(250, 40);
            inputRect.localScale = Vector3.one;
            Image inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            roomNameInput = inputObj.AddComponent<TMP_InputField>();
            
            // Input text area
            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform);
            RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);
            textAreaRect.localScale = Vector3.one;
            TextMeshProUGUI inputText = textArea.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 18;
            inputText.color = Color.white;
            roomNameInput.textComponent = inputText;
            roomNameInput.text = "MLBShowdown";
            
            // Placeholder
            GameObject placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(inputObj.transform);
            RectTransform phRect = placeholder.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(10, 5);
            phRect.offsetMax = new Vector2(-10, -5);
            phRect.localScale = Vector3.one;
            TextMeshProUGUI phText = placeholder.AddComponent<TextMeshProUGUI>();
            phText.text = "Room Name...";
            phText.fontSize = 18;
            phText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            roomNameInput.placeholder = phText;

            hostGameButton = CreateButton(onlineMenuPanel.transform, "Host Game", new Vector2(0, 10));
            joinGameButton = CreateButton(onlineMenuPanel.transform, "Join Game", new Vector2(0, -60));
            backButton = CreateButton(onlineMenuPanel.transform, "Back", new Vector2(0, -130));
            onlineMenuPanel.SetActive(false);
        }

        private GameObject CreatePanel(string name)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(transform);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            
            // Add semi-transparent background
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            
            return panel;
        }

        private Button CreateButton(Transform parent, string text, Vector2 position)
        {
            GameObject btnObj = new GameObject(text + "Button");
            btnObj.transform.SetParent(parent);
            
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(250, 50);
            rect.localScale = Vector3.one;
            
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.4f, 0.6f, 1f);
            
            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0.3f, 0.5f, 0.7f, 1f);
            colors.pressedColor = new Color(0.15f, 0.3f, 0.45f, 1f);
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
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            
            return btn;
        }

        private TextMeshProUGUI CreateText(Transform parent, string text, int fontSize, Vector2 position, TextAlignmentOptions alignment)
        {
            GameObject textObj = new GameObject("Text_" + text);
            textObj.transform.SetParent(parent);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(400, 60);
            rect.localScale = Vector3.one;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            
            return tmp;
        }

        void Update()
        {
            if (networkHandler == null)
            {
                networkHandler = NetworkRunnerHandler.Instance;
                if (networkHandler != null)
                {
                    networkHandler.OnConnectedToServerEvent += HandleConnected;
                    networkHandler.OnDisconnectedFromServerEvent += HandleDisconnected;
                    networkHandler.OnPlayerJoinedEvent += HandlePlayerJoined;
                    networkHandler.OnPlayerLeftEvent += HandlePlayerLeft;
                }
            }
            
            // Find the networked game manager - prefer spawned networked instance over local
            NetworkGameManager gameManager = null;
            
            // First try to find a spawned networked instance
            var allGameManagers = FindObjectsOfType<NetworkGameManager>();
            foreach (var gm in allGameManagers)
            {
                if (gm.Object != null && gm.Object.IsValid)
                {
                    gameManager = gm;
                    break;
                }
            }
            
            // Fall back to Instance if no networked one found
            if (gameManager == null)
            {
                gameManager = NetworkGameManager.Instance;
            }
            
            // Subscribe if we found a game manager and it's different from what we had
            if (gameManager != null && gameManager != subscribedGameManager)
            {
                Debug.Log($"[MainMenuUI] Subscribing to NetworkGameManager (isNetworked={gameManager.Object?.IsValid})");
                
                // Unsubscribe from old instance if any
                if (subscribedGameManager != null)
                {
                    subscribedGameManager.OnGameStarted -= HandleGameStarted;
                    subscribedGameManager.OnGameStateChanged -= HandleGameStateChanged;
                }
                
                gameManager.OnGameStarted += HandleGameStarted;
                gameManager.OnGameStateChanged += HandleGameStateChanged;
                subscribedGameManager = gameManager;
            }
            
            // Check if game has already started
            if (gameManager != null && gameManager.CurrentState != MLBShowdown.Core.GameState.WaitingForPlayers)
            {
                Debug.Log($"[MainMenuUI] Game already in progress (state={gameManager.CurrentState}), hiding menu");
                gameObject.SetActive(false);
            }
        }
        
        private NetworkGameManager subscribedGameManager = null;
        
        private void HandleGameStateChanged(MLBShowdown.Core.GameState newState)
        {
            Debug.Log($"[MainMenuUI] HandleGameStateChanged: {newState}");
            // Hide menu when game transitions past waiting state
            if (newState != MLBShowdown.Core.GameState.WaitingForPlayers)
            {
                Debug.Log("[MainMenuUI] Game state changed - hiding menu");
                gameObject.SetActive(false);
            }
        }
        
        private void HandleGameStarted()
        {
            Debug.Log("[MainMenuUI] HandleGameStarted called - hiding menu");
            // Hide all menu panels when game starts
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (networkHandler != null)
            {
                networkHandler.OnConnectedToServerEvent -= HandleConnected;
                networkHandler.OnDisconnectedFromServerEvent -= HandleDisconnected;
                networkHandler.OnPlayerJoinedEvent -= HandlePlayerJoined;
                networkHandler.OnPlayerLeftEvent -= HandlePlayerLeft;
            }
            
            if (subscribedGameManager != null)
            {
                subscribedGameManager.OnGameStarted -= HandleGameStarted;
                subscribedGameManager.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void SetupButtons()
        {
            if (playOnlineButton != null)
                playOnlineButton.onClick.AddListener(OnPlayOnlineClicked);
            
            if (playCPUButton != null)
                playCPUButton.onClick.AddListener(OnPlayCPUClicked);
            
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);

            if (hostGameButton != null)
                hostGameButton.onClick.AddListener(OnHostClicked);
            
            if (joinGameButton != null)
                joinGameButton.onClick.AddListener(OnJoinClicked);
            
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);

            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameClicked);
            
            if (leaveLobbyButton != null)
                leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
        }

        #region Panel Management

        private void ShowMainMenu()
        {
            SetPanelActive(mainMenuPanel, true);
            SetPanelActive(lobbyPanel, false);
            SetPanelActive(connectingPanel, false);
            SetPanelActive(onlineMenuPanel, false);
        }

        private void ShowOnlineMenu()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(lobbyPanel, false);
            SetPanelActive(connectingPanel, false);
            SetPanelActive(onlineMenuPanel, true);
        }

        private void ShowConnecting()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(lobbyPanel, false);
            SetPanelActive(connectingPanel, true);
            SetPanelActive(onlineMenuPanel, false);
        }

        private void ShowLobby()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(lobbyPanel, true);
            SetPanelActive(connectingPanel, false);
            SetPanelActive(onlineMenuPanel, false);

            UpdateLobbyUI();
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        #endregion

        #region Button Handlers

        private void OnPlayOnlineClicked()
        {
            ShowOnlineMenu();
        }

        private async void OnPlayCPUClicked()
        {
            if (networkHandler == null) return;

            ShowConnecting();
            SetStatus("Starting game...");

            bool success = await networkHandler.StartSinglePlayer();
            if (success)
            {
                // Start vs CPU game
                StartCoroutine(StartCPUGameAfterDelay());
            }
            else
            {
                SetStatus("Failed to start game");
                ShowMainMenu();
            }
        }

        private System.Collections.IEnumerator StartCPUGameAfterDelay()
        {
            yield return new WaitUntil(() => NetworkGameManager.Instance != null);
            // Use local method for single player mode
            // CPU plays home (defense first), player plays away (bats first)
            NetworkGameManager.Instance.StartGameLocal(true, true);
            gameObject.SetActive(false); // Hide menu
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private async void OnHostClicked()
        {
            // Try to find or create network handler
            if (networkHandler == null)
            {
                networkHandler = NetworkRunnerHandler.Instance;
                if (networkHandler == null)
                {
                    // Create one if it doesn't exist
                    GameObject handlerObj = new GameObject("NetworkRunnerHandler");
                    networkHandler = handlerObj.AddComponent<NetworkRunnerHandler>();
                }
            }

            if (networkHandler == null)
            {
                SetStatus("Network handler not available");
                return;
            }

            string roomName = GetRoomName();
            ShowConnecting();
            SetStatus($"Creating room '{roomName}'...");

            isHost = true;
            bool success = await networkHandler.HostGame(roomName);
            
            if (success)
            {
                ShowLobby();
                SetLobbyStatus($"Room: {roomName}\nWaiting for opponent...");
            }
            else
            {
                SetStatus("Failed to create room");
                ShowOnlineMenu();
            }
        }

        private async void OnJoinClicked()
        {
            // Try to find or create network handler
            if (networkHandler == null)
            {
                networkHandler = NetworkRunnerHandler.Instance;
                if (networkHandler == null)
                {
                    GameObject handlerObj = new GameObject("NetworkRunnerHandler");
                    networkHandler = handlerObj.AddComponent<NetworkRunnerHandler>();
                }
            }

            if (networkHandler == null)
            {
                SetStatus("Network handler not available");
                return;
            }

            string roomName = GetRoomName();
            ShowConnecting();
            SetStatus($"Joining room '{roomName}'...");

            isHost = false;
            bool success = await networkHandler.JoinGame(roomName);
            
            if (success)
            {
                ShowLobby();
                SetLobbyStatus($"Room: {roomName}\nConnected!");
            }
            else
            {
                SetStatus("Failed to join room");
                ShowOnlineMenu();
            }
        }

        private void OnBackClicked()
        {
            ShowMainMenu();
        }

        private void OnStartGameClicked()
        {
            Debug.Log($"[MainMenuUI] OnStartGameClicked - isHost={isHost}");
            if (!isHost) return;

            var gameManager = NetworkGameManager.Instance;
            Debug.Log($"[MainMenuUI] OnStartGameClicked - gameManager={gameManager}, gameManager!=null={gameManager != null}");
            if (gameManager != null)
            {
                Debug.Log("[MainMenuUI] Calling gameManager.StartGame(false, false)");
                gameManager.StartGame(false, false);
                gameObject.SetActive(false);
            }
            else
            {
                Debug.LogError("[MainMenuUI] NetworkGameManager.Instance is null!");
            }
        }

        private void OnLeaveLobbyClicked()
        {
            if (networkHandler != null)
            {
                networkHandler.Disconnect();
            }
            ShowMainMenu();
        }

        #endregion

        #region Network Event Handlers

        private void HandleConnected()
        {
            Debug.Log("Connected to server");
        }

        private void HandleDisconnected()
        {
            ShowMainMenu();
            SetStatus("Disconnected from server");
        }

        private void HandlePlayerJoined(Fusion.PlayerRef player)
        {
            UpdateLobbyUI();
        }

        private void HandlePlayerLeft(Fusion.PlayerRef player)
        {
            UpdateLobbyUI();
        }

        #endregion

        #region UI Helpers

        private string GetRoomName()
        {
            if (roomNameInput != null && !string.IsNullOrEmpty(roomNameInput.text))
            {
                return roomNameInput.text;
            }
            return "MLBShowdown_" + Random.Range(1000, 9999);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log($"[Menu] {message}");
        }

        private void SetLobbyStatus(string message)
        {
            if (lobbyStatusText != null) lobbyStatusText.text = message;
        }

        private void UpdateLobbyUI()
        {
            if (networkHandler == null || networkHandler.Runner == null) return;

            int playerCount = networkHandler.Runner.ActivePlayers.Count();
            if (playerCountText != null)
            {
                playerCountText.text = $"Players: {playerCount}/2";
            }

            // Enable start button only for host with 2 players
            if (startGameButton != null)
            {
                startGameButton.interactable = isHost && playerCount >= 2;
            }

            if (lobbyStatusText != null && playerCount >= 2)
            {
                SetLobbyStatus("Both players connected!\n" + (isHost ? "Press Start to begin!" : "Waiting for host..."));
            }
        }

        #endregion
    }
}
