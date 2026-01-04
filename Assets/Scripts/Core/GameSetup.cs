using UnityEngine;
using MLBShowdown.Network;
using MLBShowdown.Dice;
using MLBShowdown.BaseRunning;
using MLBShowdown.UI;

namespace MLBShowdown.Core
{
    public class GameSetup : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject networkRunnerHandlerPrefab;
        [SerializeField] private GameObject dicePhysicsSetupPrefab;
        [SerializeField] private GameObject gameUIPrefab;

        [Header("Scene References")]
        [SerializeField] private Transform diceAreaTransform;
        [SerializeField] private Canvas mainCanvas;

        private NetworkRunnerHandler networkHandler;
        private DicePhysicsSetup dicePhysics;
        private GameUI gameUI;

        void Awake()
        {
            SetupScene();
        }

        private void SetupScene()
        {
            // Create Network Runner Handler
            if (NetworkRunnerHandler.Instance == null)
            {
                GameObject handlerObj;
                if (networkRunnerHandlerPrefab != null)
                {
                    handlerObj = Instantiate(networkRunnerHandlerPrefab);
                }
                else
                {
                    handlerObj = new GameObject("NetworkRunnerHandler");
                    handlerObj.AddComponent<NetworkRunnerHandler>();
                }
                networkHandler = handlerObj.GetComponent<NetworkRunnerHandler>();
            }
            else
            {
                networkHandler = NetworkRunnerHandler.Instance;
            }

            // Create Dice Physics Area
            SetupDiceArea();

            // Create UI
            SetupUI();

            // Setup Camera
            SetupCamera();
        }

        private void SetupDiceArea()
        {
            GameObject diceAreaObj;
            if (dicePhysicsSetupPrefab != null)
            {
                diceAreaObj = Instantiate(dicePhysicsSetupPrefab);
            }
            else
            {
                diceAreaObj = new GameObject("DiceArea");
                diceAreaObj.AddComponent<DicePhysicsSetup>();
            }

            if (diceAreaTransform != null)
            {
                diceAreaObj.transform.position = diceAreaTransform.position;
            }
            else
            {
                diceAreaObj.transform.position = new Vector3(0, 0, 0);
            }

            dicePhysics = diceAreaObj.GetComponent<DicePhysicsSetup>();
        }

        private void SetupUI()
        {
            // Find or create main canvas
            if (mainCanvas == null)
            {
                mainCanvas = FindObjectOfType<Canvas>();
                if (mainCanvas == null)
                {
                    GameObject canvasObj = new GameObject("MainCanvas");
                    mainCanvas = canvasObj.AddComponent<Canvas>();
                    mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                    canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }
            }

            // Create Game UI
            if (gameUIPrefab != null)
            {
                GameObject uiObj = Instantiate(gameUIPrefab, mainCanvas.transform);
                gameUI = uiObj.GetComponent<GameUI>();
            }
            else
            {
                // Create basic UI programmatically
                CreateBasicUI();
            }
        }

        private void CreateBasicUI()
        {
            GameObject uiObj = new GameObject("GameUI");
            uiObj.transform.SetParent(mainCanvas.transform);
            
            RectTransform rect = uiObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            gameUI = uiObj.AddComponent<GameUI>();

            // The GameUI component will create its own UI elements if references are null
            // For a complete implementation, we'd create all UI elements here
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

            // Position camera to view the dice area (2D-style top-down view)
            mainCam.transform.position = new Vector3(0, 8, -5);
            mainCam.transform.rotation = Quaternion.Euler(50, 0, 0);
            mainCam.orthographic = false;
            mainCam.fieldOfView = 60;
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 100f;
            mainCam.backgroundColor = new Color(0.1f, 0.15f, 0.2f);
            mainCam.clearFlags = CameraClearFlags.SolidColor;
        }

        public DicePhysicsSetup GetDicePhysics() => dicePhysics;
        public GameUI GetGameUI() => gameUI;
    }
}
