using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.UI;
using MLBShowdown.Network;
using MLBShowdown.Dice;
using MLBShowdown.UI;

namespace MLBShowdown.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Scene Setup")]
        [SerializeField] private bool autoCreateUI = true;
        [SerializeField] private bool autoCreateDiceArea = true;

        private static GameBootstrap instance;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeGame();
        }

        private void InitializeGame()
        {
            // Ensure NetworkRunnerHandler exists
            if (NetworkRunnerHandler.Instance == null)
            {
                GameObject handlerObj = new GameObject("NetworkRunnerHandler");
                handlerObj.AddComponent<NetworkRunnerHandler>();
            }

            // Create aspect ratio enforcer for landscape phone display
            CreateAspectRatioEnforcer();

            // Create dice physics area
            if (autoCreateDiceArea)
            {
                CreateDiceArea();
            }

            // Create UI
            if (autoCreateUI)
            {
                CreateGameUI();
            }

            // Setup camera
            SetupCamera();

            // Add lighting
            SetupLighting();
        }

        private void CreateAspectRatioEnforcer()
        {
            if (FindObjectOfType<AspectRatioEnforcer>() == null)
            {
                GameObject aspectObj = new GameObject("AspectRatioEnforcer");
                aspectObj.AddComponent<AspectRatioEnforcer>();
            }
        }

        private void CreateDiceArea()
        {
            GameObject diceAreaObj = new GameObject("DiceArea");
            diceAreaObj.AddComponent<DicePhysicsSetup>();
            diceAreaObj.transform.position = Vector3.zero;

            // Add dice roller
            GameObject diceRollerObj = new GameObject("DiceRoller");
            diceRollerObj.transform.SetParent(diceAreaObj.transform);
            diceRollerObj.AddComponent<DiceRoller3D>();
        }

        private void CreateGameUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("GameCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Add Event System if not present
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<InputSystemUIInputModule>();
            }

            // Create Main Menu UI
            CreateMainMenuUI(canvasObj.transform);

            // Create Game HUD (hidden initially)
            CreateGameHUD(canvasObj.transform);
        }

        private void CreateMainMenuUI(Transform parent)
        {
            GameObject menuObj = new GameObject("MainMenu");
            menuObj.transform.SetParent(parent);

            RectTransform rect = menuObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Background
            var bg = menuObj.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.1f, 0.15f, 0.2f, 0.95f);

            // Title
            CreateTextElement(menuObj.transform, "Title", "MLB SHOWDOWN", 
                new Vector2(0.5f, 0.85f), 72, TMPro.FontStyles.Bold);

            // Subtitle
            CreateTextElement(menuObj.transform, "Subtitle", "Card Battle Baseball", 
                new Vector2(0.5f, 0.75f), 32, TMPro.FontStyles.Italic);

            // Buttons
            CreateButton(menuObj.transform, "HostButton", "HOST GAME", new Vector2(0.5f, 0.55f), OnHostGame);
            CreateButton(menuObj.transform, "JoinButton", "JOIN GAME", new Vector2(0.5f, 0.45f), OnJoinGame);
            CreateButton(menuObj.transform, "CPUButton", "VS CPU", new Vector2(0.5f, 0.35f), OnVsCPU);

            // Room name input
            CreateInputField(menuObj.transform, "RoomInput", "Room Name...", new Vector2(0.5f, 0.25f));

            menuObj.AddComponent<MainMenuUI>();
        }

        private void CreateGameHUD(Transform parent)
        {
            GameObject hudObj = new GameObject("GameHUD");
            hudObj.transform.SetParent(parent);
            hudObj.SetActive(false);

            RectTransform rect = hudObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Scoreboard (top center)
            CreateScoreboard(hudObj.transform);

            // Current at-bat info (center)
            CreateAtBatDisplay(hudObj.transform);

            // Base runner diamond (bottom right)
            CreateBaseRunnerDiamond(hudObj.transform);

            // Roll button (bottom center)
            CreateButton(hudObj.transform, "RollButton", "ROLL DICE", new Vector2(0.5f, 0.15f), OnRollDice);

            // Game message area
            CreateTextElement(hudObj.transform, "GameMessage", "", new Vector2(0.5f, 0.5f), 36, TMPro.FontStyles.Bold);

            hudObj.AddComponent<GameUI>();
        }

        private void CreateScoreboard(Transform parent)
        {
            GameObject scoreboardObj = new GameObject("Scoreboard");
            scoreboardObj.transform.SetParent(parent);

            RectTransform rect = scoreboardObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.3f, 0.9f);
            rect.anchorMax = new Vector2(0.7f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = scoreboardObj.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // Inning
            CreateTextElement(scoreboardObj.transform, "Inning", "Top 1", new Vector2(0.5f, 0.7f), 24);
            
            // Scores
            CreateTextElement(scoreboardObj.transform, "AwayLabel", "AWAY", new Vector2(0.25f, 0.7f), 18);
            CreateTextElement(scoreboardObj.transform, "AwayScore", "0", new Vector2(0.25f, 0.3f), 36, TMPro.FontStyles.Bold);
            
            CreateTextElement(scoreboardObj.transform, "HomeLabel", "HOME", new Vector2(0.75f, 0.7f), 18);
            CreateTextElement(scoreboardObj.transform, "HomeScore", "0", new Vector2(0.75f, 0.3f), 36, TMPro.FontStyles.Bold);

            // Outs
            CreateTextElement(scoreboardObj.transform, "Outs", "0 Outs", new Vector2(0.5f, 0.3f), 20);
        }

        private void CreateAtBatDisplay(Transform parent)
        {
            GameObject atBatObj = new GameObject("AtBatDisplay");
            atBatObj.transform.SetParent(parent);

            RectTransform rect = atBatObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.35f, 0.6f);
            rect.anchorMax = new Vector2(0.65f, 0.85f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = atBatObj.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.85f);

            // Pitcher info
            CreateTextElement(atBatObj.transform, "PitcherName", "Pitcher", new Vector2(0.5f, 0.85f), 20);
            CreateTextElement(atBatObj.transform, "PitcherStats", "CTRL: 0", new Vector2(0.5f, 0.75f), 16);

            // VS
            CreateTextElement(atBatObj.transform, "VS", "VS", new Vector2(0.5f, 0.55f), 24, TMPro.FontStyles.Bold);

            // Batter info
            CreateTextElement(atBatObj.transform, "BatterName", "Batter", new Vector2(0.5f, 0.35f), 20);
            CreateTextElement(atBatObj.transform, "BatterStats", "OB: 0 | SPD: 0", new Vector2(0.5f, 0.25f), 16);

            // Advantage indicator
            CreateTextElement(atBatObj.transform, "Advantage", "", new Vector2(0.5f, 0.1f), 18, TMPro.FontStyles.Bold);
        }

        private void CreateBaseRunnerDiamond(Transform parent)
        {
            GameObject diamondObj = new GameObject("BaseRunnerDiamond");
            diamondObj.transform.SetParent(parent);

            RectTransform rect = diamondObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.8f, 0.05f);
            rect.anchorMax = new Vector2(0.95f, 0.25f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            diamondObj.AddComponent<BaseRunnerDisplay>();
        }

        private TMPro.TextMeshProUGUI CreateTextElement(Transform parent, string name, string text, 
            Vector2 anchorPos, int fontSize, TMPro.FontStyles style = TMPro.FontStyles.Normal)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorPos;
            rect.anchorMax = anchorPos;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(400, 50);

            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return tmp;
        }

        private UnityEngine.UI.Button CreateButton(Transform parent, string name, string text, 
            Vector2 anchorPos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorPos;
            rect.anchorMax = anchorPos;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(250, 60);

            var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.4f, 0.6f);

            var button = buttonObj.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            return button;
        }

        private TMPro.TMP_InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 anchorPos)
        {
            GameObject inputObj = new GameObject(name);
            inputObj.transform.SetParent(parent);

            RectTransform rect = inputObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorPos;
            rect.anchorMax = anchorPos;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(300, 50);

            var image = inputObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f);

            // Text area
            GameObject textAreaObj = new GameObject("TextArea");
            textAreaObj.transform.SetParent(inputObj.transform);

            RectTransform textAreaRect = textAreaObj.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);

            // Placeholder
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(textAreaObj.transform);

            RectTransform phRect = placeholderObj.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = Vector2.zero;
            phRect.offsetMax = Vector2.zero;

            var phText = placeholderObj.AddComponent<TMPro.TextMeshProUGUI>();
            phText.text = placeholder;
            phText.fontSize = 20;
            phText.fontStyle = TMPro.FontStyles.Italic;
            phText.color = new Color(0.5f, 0.5f, 0.5f);

            // Input text
            GameObject inputTextObj = new GameObject("Text");
            inputTextObj.transform.SetParent(textAreaObj.transform);

            RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;

            var inputText = inputTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            inputText.fontSize = 20;
            inputText.color = Color.white;

            var inputField = inputObj.AddComponent<TMPro.TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = inputText;
            inputField.placeholder = phText;

            return inputField;
        }

        private void SetupCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("MainCamera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            // Position for viewing dice table from above at an angle
            mainCam.transform.position = new Vector3(0, 6, -4);
            mainCam.transform.rotation = Quaternion.Euler(45, 0, 0);
            mainCam.fieldOfView = 60;
            mainCam.backgroundColor = new Color(0.05f, 0.1f, 0.15f);
            mainCam.clearFlags = CameraClearFlags.SolidColor;
        }

        private void SetupLighting()
        {
            // Main directional light
            if (FindObjectOfType<Light>() == null)
            {
                GameObject lightObj = new GameObject("DirectionalLight");
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                light.color = new Color(1f, 0.95f, 0.9f);
                lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
        }

        #region Button Callbacks

        private void OnHostGame()
        {
            Debug.Log("Host Game clicked");
        }

        private void OnJoinGame()
        {
            Debug.Log("Join Game clicked");
        }

        private void OnVsCPU()
        {
            Debug.Log("VS CPU clicked");
        }

        private void OnRollDice()
        {
            Debug.Log("Roll Dice clicked");
        }

        #endregion
    }
}
