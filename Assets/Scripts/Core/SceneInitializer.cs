using UnityEngine;
using UnityEngine.InputSystem.UI;
using MLBShowdown.Network;
using MLBShowdown.Dice;
using MLBShowdown.BaseRunning;
using MLBShowdown.UI;

namespace MLBShowdown.Core
{
    [DefaultExecutionOrder(-100)]
    public class SceneInitializer : MonoBehaviour
    {
        [Header("Auto-Create Settings")]
        [SerializeField] private bool createNetworkHandler = true;
        [SerializeField] private bool createDiceSystem = true;
        [SerializeField] private bool createUI = true;
        [SerializeField] private bool createLighting = true;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (createNetworkHandler)
            {
                SetupNetworkHandler();
            }

            if (createDiceSystem)
            {
                SetupDiceSystem();
            }

            if (createUI)
            {
                SetupUI();
            }

            if (createLighting)
            {
                SetupLighting();
            }

            SetupCamera();
        }

        private void SetupNetworkHandler()
        {
            if (NetworkRunnerHandler.Instance != null) return;

            GameObject handlerObj = new GameObject("NetworkRunnerHandler");
            handlerObj.AddComponent<NetworkRunnerHandler>();
            DontDestroyOnLoad(handlerObj);
        }

        private void SetupDiceSystem()
        {
            if (FindObjectOfType<DicePhysicsSetup>() != null) return;

            var diceSystem = DicePrefabCreator.CreateDiceRollerSetup();
            diceSystem.transform.position = Vector3.zero;
        }

        private void SetupUI()
        {
            // Check for existing canvas
            Canvas existingCanvas = FindObjectOfType<Canvas>();
            if (existingCanvas != null) return;

            // Create main canvas
            GameObject canvasObj = new GameObject("MainCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Event system
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventObj = new GameObject("EventSystem");
                eventObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventObj.AddComponent<InputSystemUIInputModule>();
            }

            // Add UI components
            CreateMainMenu(canvasObj.transform);
            CreateGameHUD(canvasObj.transform);
            CreateGameboard(canvasObj.transform);
            CreateGameOverScreen(canvasObj.transform);
        }

        private void CreateMainMenu(Transform parent)
        {
            GameObject menuObj = new GameObject("MainMenuUI");
            menuObj.transform.SetParent(parent);

            RectTransform rect = menuObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            menuObj.AddComponent<MainMenuUI>();
        }

        private void CreateGameHUD(Transform parent)
        {
            GameObject hudObj = new GameObject("GameUI");
            hudObj.transform.SetParent(parent);

            RectTransform rect = hudObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            hudObj.AddComponent<GameUI>();
            // Keep active - GameUI will show/hide its own panels based on game state
        }

        private void CreateGameboard(Transform parent)
        {
            // Create 3D game board (Hearthstone style) instead of 2D UI
            GameObject gameboardObj = new GameObject("GameBoard3D");
            // Don't parent to canvas - this is a 3D object
            gameboardObj.AddComponent<GameBoard3D>();
        }

        private void CreateGameOverScreen(Transform parent)
        {
            GameObject gameOverObj = new GameObject("GameOverUI");
            gameOverObj.transform.SetParent(parent);

            RectTransform rect = gameOverObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            gameOverObj.AddComponent<GameOverUI>();
        }

        private void SetupLighting()
        {
            if (FindObjectOfType<Light>() != null) return;

            // Main directional light
            GameObject lightObj = new GameObject("DirectionalLight");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Ambient light
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.35f, 0.4f);
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

            // Position camera for 2D-style view of 3D dice
            mainCam.transform.position = new Vector3(0, 7, -5);
            mainCam.transform.rotation = Quaternion.Euler(50, 0, 0);
            mainCam.fieldOfView = 50;
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 100f;
            mainCam.backgroundColor = new Color(0.08f, 0.12f, 0.18f);
            mainCam.clearFlags = CameraClearFlags.SolidColor;
        }
    }
}
