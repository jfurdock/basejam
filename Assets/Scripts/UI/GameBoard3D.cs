using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using MLBShowdown.Network;
using MLBShowdown.Cards;
using MLBShowdown.Core;
using MLBShowdown.Dice;
using Random = UnityEngine.Random;

namespace MLBShowdown.UI
{
    public class GameBoard3D : MonoBehaviour
    {
        [Header("Board Dimensions")]
        [SerializeField] private float boardWidth = 20f;
        [SerializeField] private float boardHeight = 14f;
        [SerializeField] private float cardWidth = 1.0f;
        [SerializeField] private float cardHeight = 1.4f;
        
        [Header("Camera Settings")]
        [SerializeField] private float cameraHeight = 18f;
        [SerializeField] private float cameraAngle = 55f;
        
        [Header("Dice Settings")]
        [SerializeField] private float diceScale = 1.3f;
        [SerializeField] private float diceThrowForce = 7f;
        [SerializeField] private float diceTorque = 320f;

        [Header("Animation Settings")]
        [SerializeField] private float moveDuration = 0.4f;
        [SerializeField] private float baseRunningDelay = 0.25f;
        [SerializeField] private float cardHeightOffset = 0.05f;
        [SerializeField] private Color activeBatterColor = new Color(1f, 0.9f, 0.5f);
        
        private NetworkGameManager gameManager;
        private Camera mainCamera;
        private GameObject boardRoot;
        
        private List<GameObject> topLineupCards = new List<GameObject>();
        private List<GameObject> bottomLineupCards = new List<GameObject>();
        private GameObject topPitcherCard;
        private GameObject bottomPitcherCard;
        private GameObject batterCard;
        private GameObject pitcherOnMoundCard;
        
        private TextMeshPro scoreText;
        private TextMeshPro inningText;
        private TextMeshPro outsText;
        private TextMeshPro turnText;
        private TextMeshPro messageText;
        
        private Vector3 homePlatePos;
        private Vector3 firstBasePos;
        private Vector3 secondBasePos;
        private Vector3 thirdBasePos;
        private Vector3 homeCardPos;
        private Vector3 firstCardPos;
        private Vector3 secondCardPos;
        private Vector3 thirdCardPos;
        private Vector3 pitcherMoundPos;
        
        private GameObject currentDice;
        private Rigidbody diceRigidbody;
        private bool isRollingDice = false;
        
        private Material greenMat;
        private Material brownMat;
        private Material redMat;
        private Material blueMat;
        private Material whiteMat;
        private Material grayMat;

        private bool pitcherOnMound;
        private bool currentPitcherIsHome;
        private bool isAnimatingPitcher;
        private bool isAnimatingRunners;
        private AtBatOutcome? pendingOutcome;
        private DiceRoller3D diceRoller;
        private string lastRollLabel = "ROLL";
        private int? pendingDiceResult;

        private TextMeshPro rollResultText;
        private TextMeshPro outcomeText;
        private GameObject scorebugPanel;
        private GameObject rollOutcomePanel;

        private class RunnerInfo
        {
            public string PlayerName;
            public int Speed;
            public int LineupIndex;
            public bool IsAwayTeam;
            public GameObject Card;
        }

        private RunnerInfo runnerOnFirst;
        private RunnerInfo runnerOnSecond;
        private RunnerInfo runnerOnThird;
        private RunnerInfo currentBatterInfo;

        private HashSet<int> awayPlayersOnField = new HashSet<int>();
        private HashSet<int> homePlayersOnField = new HashSet<int>();
        
        void Start()
        {
            StartCoroutine(InitializeWhenReady());
        }
        
        private IEnumerator InitializeWhenReady()
        {
            while (NetworkGameManager.Instance == null)
                yield return new WaitForSeconds(0.1f);
            
            gameManager = NetworkGameManager.Instance;
            CreateMaterials();
            SetupCamera();
            CreateBoard();
            SubscribeToEvents();

            diceRoller = FindObjectOfType<DiceRoller3D>();
            if (diceRoller != null)
            {
                diceRoller.OnDiceRollStarted += HandleDiceRollStarted;
                diceRoller.OnDiceRollComplete += HandleDiceRollComplete;
            }
            
            yield return new WaitForSeconds(0.5f);
            RefreshBoard();
        }
        
        private void CreateMaterials()
        {
            // Use Unlit/Color shader which always works
            Shader unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader == null) unlitShader = Shader.Find("UI/Default");
            
            greenMat = new Material(unlitShader);
            greenMat.color = new Color(0.15f, 0.4f, 0.15f);
            
            brownMat = new Material(unlitShader);
            brownMat.color = new Color(0.55f, 0.35f, 0.2f);
            
            redMat = new Material(unlitShader);
            redMat.color = new Color(0.6f, 0.15f, 0.15f);
            
            blueMat = new Material(unlitShader);
            blueMat.color = new Color(0.15f, 0.25f, 0.5f);
            
            whiteMat = new Material(unlitShader);
            whiteMat.color = Color.white;
            
            grayMat = new Material(unlitShader);
            grayMat.color = new Color(0.3f, 0.3f, 0.35f);
        }
        
        private void SetupCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject camObj = new GameObject("MainCamera");
                mainCamera = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }
            
            // Directly overhead view (looking straight down)
            mainCamera.transform.position = new Vector3(0, cameraHeight, 0);
            mainCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = boardHeight * 0.55f;
            mainCamera.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
        }
        
        private void CreateBoard()
        {
            boardRoot = new GameObject("GameBoard3D");
            boardRoot.transform.position = Vector3.zero;
            
            // Main green field
            GameObject field = GameObject.CreatePrimitive(PrimitiveType.Quad);
            field.name = "Field";
            field.transform.SetParent(boardRoot.transform);
            field.transform.localPosition = Vector3.zero;
            field.transform.localRotation = Quaternion.Euler(90, 0, 0);
            field.transform.localScale = new Vector3(boardWidth, boardHeight, 1);
            field.GetComponent<Renderer>().material = greenMat;
            
            // Add collider for dice
            BoxCollider fieldCollider = field.AddComponent<BoxCollider>();
            fieldCollider.size = new Vector3(1, 1, 0.1f);
            PhysicsMaterial pm = new PhysicsMaterial();
            pm.bounciness = 0.3f;
            pm.dynamicFriction = 0.7f;
            fieldCollider.material = pm;
            
            // Diamond in center
            CreateDiamond();
            
            // Lineup areas
            CreateLineups();
            
            // UI Text
            CreateUIText();
            
            // Invisible walls for dice
            CreateWalls();
        }
        
        private void CreateDiamond()
        {
            float diamondSize = 2.5f;
            float centerZ = 0f;
            
            homePlatePos = new Vector3(0, 0.02f, centerZ - diamondSize);
            firstBasePos = new Vector3(diamondSize, 0.02f, centerZ);
            secondBasePos = new Vector3(0, 0.02f, centerZ + diamondSize);
            thirdBasePos = new Vector3(-diamondSize, 0.02f, centerZ);
            
            // Infield dirt (diamond shape using quad)
            GameObject infield = GameObject.CreatePrimitive(PrimitiveType.Quad);
            infield.name = "Infield";
            infield.transform.SetParent(boardRoot.transform);
            infield.transform.localPosition = new Vector3(0, 0.01f, centerZ);
            infield.transform.localRotation = Quaternion.Euler(90, 45, 0);
            infield.transform.localScale = new Vector3(diamondSize * 2.5f, diamondSize * 2.5f, 1);
            infield.GetComponent<Renderer>().material = brownMat;
            Destroy(infield.GetComponent<Collider>());
            
            // Bases
            CreateBase("Home", homePlatePos);
            CreateBase("First", firstBasePos);
            CreateBase("Second", secondBasePos);
            CreateBase("Third", thirdBasePos);

            homeCardPos = homePlatePos + Vector3.up * cardHeightOffset;
            firstCardPos = firstBasePos + Vector3.up * cardHeightOffset;
            secondCardPos = secondBasePos + Vector3.up * cardHeightOffset;
            thirdCardPos = thirdBasePos + Vector3.up * cardHeightOffset;
            pitcherMoundPos = Vector3.Lerp(homePlatePos, secondBasePos, 0.45f) + Vector3.up * cardHeightOffset;
        }
        
        private void CreateBase(string name, Vector3 pos)
        {
            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            baseObj.name = name + "Base";
            baseObj.transform.SetParent(boardRoot.transform);
            baseObj.transform.localPosition = pos;
            baseObj.transform.localRotation = Quaternion.Euler(90, 45, 0);
            baseObj.transform.localScale = new Vector3(0.4f, 0.4f, 1);
            baseObj.GetComponent<Renderer>().material = whiteMat;
            Destroy(baseObj.GetComponent<Collider>());
        }
        
        private void CreateLineups()
        {
            float topZ = boardHeight * 0.4f;
            float bottomZ = -boardHeight * 0.48f;
            float startX = -boardWidth * 0.4f;
            float spacing = cardWidth * 1.15f;
            
            for (int i = 0; i < 9; i++)
            {
                float x = startX + i * spacing;
                
                // Top lineup (opponent)
                GameObject topCard = CreateCard("TopCard_" + i, new Vector3(x, 0.02f, topZ), blueMat);
                topLineupCards.Add(topCard);
                
                // Bottom lineup (player)
                GameObject bottomCard = CreateCard("BottomCard_" + i, new Vector3(x, 0.02f, bottomZ), redMat);
                bottomLineupCards.Add(bottomCard);
            }
            
            // Pitcher cards on right side
            float pitcherX = boardWidth * 0.4f;
            topPitcherCard = CreateCard("TopPitcher", new Vector3(pitcherX, 0.02f, topZ * 0.5f), blueMat);
            bottomPitcherCard = CreateCard("BottomPitcher", new Vector3(pitcherX, 0.02f, bottomZ * 0.5f), redMat);
        }
        
        private GameObject CreateCard(string name, Vector3 pos, Material mat)
        {
            GameObject card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = name;
            card.transform.SetParent(boardRoot.transform);
            card.transform.localPosition = pos;
            card.transform.localRotation = Quaternion.Euler(90, 0, 0);
            card.transform.localScale = new Vector3(cardWidth, cardHeight, 1);
            card.GetComponent<Renderer>().material = mat;
            Destroy(card.GetComponent<Collider>());
            
            // Add text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(card.transform);
            textObj.transform.localPosition = new Vector3(0, 0, -0.01f);
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = new Vector3(0.75f, 0.5f, 1);
            
            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.text = "";
            tmp.fontSize = 2.8f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableWordWrapping = false;
            
            return card;
        }

        private GameObject CreateFieldCard(string name, Vector3 pos, Color color)
        {
            GameObject card = CreateCard(name, pos, whiteMat);
            var renderer = card.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
            Transform textTransform = card.transform.Find("Text");
            if (textTransform != null)
            {
                textTransform.localScale = new Vector3(0.9f, 0.7f, 1);
                var tmp = textTransform.GetComponent<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.fontSize = 5.2f;
                    tmp.enableWordWrapping = false;
                }
            }
            return card;
        }
        
        private void CreateUIText()
        {
            // Scorebug panel (top-right)
            GameObject scorebugRoot = new GameObject("Scorebug");
            scorebugRoot.transform.SetParent(boardRoot.transform);
            scorebugRoot.transform.localPosition = new Vector3(boardWidth * 0.42f, 0.1f, boardHeight * 0.46f);
            scorebugRoot.transform.localRotation = Quaternion.Euler(90, 0, 0);

            scorebugPanel = CreateHudPanel(scorebugRoot.transform, "ScorebugPanel", new Vector2(5.8f, 2.1f),
                new Color(0.05f, 0.05f, 0.08f, 0.9f));
            scoreText = CreateWorldText(scorebugRoot.transform, "ScoreText", new Vector3(0, 0.7f, -0.01f),
                "YOU 0   OPP 0", 6.5f, TextAlignmentOptions.Center);
            inningText = CreateWorldText(scorebugRoot.transform, "InningText", new Vector3(0, 0.0f, -0.01f),
                "TOP 1", 5.6f, TextAlignmentOptions.Center);
            outsText = CreateWorldText(scorebugRoot.transform, "OutsText", new Vector3(0, -0.7f, -0.01f),
                "0 OUTS", 4.8f, TextAlignmentOptions.Center);

            // Roll + outcome panel (bottom center)
            GameObject rollRoot = new GameObject("RollOutcome");
            rollRoot.transform.SetParent(boardRoot.transform);
            rollRoot.transform.localPosition = new Vector3(0, 0.1f, -boardHeight * 0.2f);
            rollRoot.transform.localRotation = Quaternion.Euler(90, 0, 0);

            rollOutcomePanel = CreateHudPanel(rollRoot.transform, "RollOutcomePanel", new Vector2(8.5f, 2.4f),
                new Color(0.05f, 0.05f, 0.08f, 0.85f));
            rollResultText = CreateWorldText(rollRoot.transform, "RollResultText", new Vector3(0, 0.8f, -0.01f),
                "ROLL: --", 6.2f, TextAlignmentOptions.Center);
            outcomeText = CreateWorldText(rollRoot.transform, "OutcomeText", new Vector3(0, -0.1f, -0.01f),
                "OUTCOME: --", 5.6f, TextAlignmentOptions.Center);
            turnText = CreateWorldText(rollRoot.transform, "TurnText", new Vector3(0, -1.1f, -0.01f),
                "YOUR TURN", 4f, TextAlignmentOptions.Center);

            // Message - bottom center (smaller)
            messageText = CreateWorldText("Message", new Vector3(0, 0.1f, -boardHeight * 0.52f), "", 2.6f);
        }
        
        private TextMeshPro CreateWorldText(string name, Vector3 pos, string text, float fontSize)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(boardRoot.transform);
            obj.transform.localPosition = pos;
            obj.transform.localRotation = Quaternion.Euler(90, 0, 0);

            TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return tmp;
        }

        private TextMeshPro CreateWorldText(Transform parent, string name, Vector3 localPos, string text, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = Quaternion.identity;

            TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;

            return tmp;
        }

        private GameObject CreateHudPanel(Transform parent, string name, Vector2 size, Color color)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = name;
            panel.transform.SetParent(parent);
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = panel.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Unlit/Color"));
                if (mat.shader == null) mat = new Material(Shader.Find("UI/Default"));
                mat.color = color;
                renderer.material = mat;
            }

            Destroy(panel.GetComponent<Collider>());
            return panel;
        }
        
        private void CreateWalls()
        {
            float wallHeight = 6f;
            CreateWall("Front", new Vector3(0, wallHeight / 2f, -boardHeight / 2f - 0.35f), new Vector3(boardWidth + 2, wallHeight, 1));
            CreateWall("Back", new Vector3(0, wallHeight / 2f, boardHeight / 2f + 0.35f), new Vector3(boardWidth + 2, wallHeight, 1));
            CreateWall("Left", new Vector3(-boardWidth / 2f - 0.35f, wallHeight / 2f, 0), new Vector3(1, wallHeight, boardHeight + 2));
            CreateWall("Right", new Vector3(boardWidth / 2f + 0.35f, wallHeight / 2f, 0), new Vector3(1, wallHeight, boardHeight + 2));
        }
        
        private void CreateWall(string name, Vector3 pos, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall_" + name;
            wall.transform.SetParent(boardRoot.transform);
            wall.transform.localPosition = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().enabled = false;
        }
        
        private void SubscribeToEvents()
        {
            if (gameManager == null) return;
            gameManager.OnGameStarted += HandleGameStarted;
            gameManager.OnAtBatStarted += HandleAtBatStarted;
            gameManager.OnAtBatEnded += HandleAtBatEnded;
            gameManager.OnScoreChanged += (h, a) => UpdateScore(h, a);
            gameManager.OnInningChanged += HandleInningChanged;
            gameManager.OnOutsChanged += (o) => UpdateOuts(o);
            gameManager.OnGameMessage += (m) => ShowMessage(m);
            gameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void HandleGameStarted()
        {
            RefreshBoard();
        }

        private void HandleAtBatStarted(int batterIndex)
        {
            if (outcomeText != null)
            {
                outcomeText.text = "OUTCOME: --";
            }
            StartCoroutine(BringUpNextBatter());
        }

        private void HandleAtBatEnded(AtBatOutcome outcome)
        {
            if (outcomeText != null)
            {
                outcomeText.text = $"OUTCOME: {AtBatController.GetOutcomeDisplayText(outcome)}";
            }

            if (isAnimatingRunners)
            {
                pendingOutcome = outcome;
                return;
            }

            StartCoroutine(WaitForBatterCardAndAnimate(outcome));
        }

        private void HandleInningChanged(int inning, bool isTop)
        {
            StartCoroutine(AnimateInningChange());
        }

        private void HandleGameStateChanged(GameState newState)
        {
            RefreshBoard();
            if (newState == GameState.DefenseTurn && currentBatterInfo == null)
            {
                StartCoroutine(BringUpNextBatter());
            }
        }

        private void HandleDiceRollStarted()
        {
            if (gameManager != null)
            {
                lastRollLabel = gameManager.CurrentState switch
                {
                    GameState.DefenseTurn => "PITCHER ROLL",
                    GameState.OffenseTurn => "BATTER ROLL",
                    GameState.OptionalAction => "OPTIONAL ROLL",
                    _ => "ROLL"
                };
            }

            if (rollResultText != null)
            {
                rollResultText.text = $"{lastRollLabel}: ...";
            }

            if (!isRollingDice)
            {
                RollDice();
            }
        }

        private void HandleDiceRollComplete(int result)
        {
            if (rollResultText != null)
            {
                rollResultText.text = $"{lastRollLabel}: {result}";
            }

            pendingDiceResult = result;
            TryApplyDiceResult();
        }

        private IEnumerator BringUpNextBatter()
        {
            while (isAnimatingRunners)
            {
                yield return null;
            }

            var batter = gameManager?.GetCurrentBatter();
            if (batter == null) yield break;

            int lineupIndex = gameManager.GetCurrentBatterLineupIndex();
            bool isAway = gameManager.IsTopOfInning;

            currentBatterInfo = new RunnerInfo
            {
                PlayerName = batter.PlayerName,
                Speed = batter.Speed,
                LineupIndex = lineupIndex,
                IsAwayTeam = isAway
            };

            GetPlayersOnFieldForTeam(isAway).Add(lineupIndex);

            yield return StartCoroutine(AnimateBatterToPlate());

            if (pendingOutcome.HasValue)
            {
                var outcome = pendingOutcome.Value;
                pendingOutcome = null;
                yield return StartCoroutine(AnimateAtBatResult(outcome));
            }
        }

        private IEnumerator WaitForBatterCardAndAnimate(AtBatOutcome outcome)
        {
            float timeout = 1.5f;
            while (timeout > 0f && (currentBatterInfo == null || (currentBatterInfo.Card == null && batterCard == null)))
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (currentBatterInfo == null)
            {
                pendingOutcome = outcome;
                yield break;
            }

            if (currentBatterInfo.Card == null && batterCard != null)
            {
                currentBatterInfo.Card = batterCard;
            }
            if (currentBatterInfo.Card == null)
            {
                var batter = gameManager.GetCurrentBatter();
                batterCard = CreateFieldCard("BatterCard", homeCardPos, activeBatterColor);
                UpdateBatterCardDisplay(batterCard, batter);
                currentBatterInfo.Card = batterCard;
            }

            yield return StartCoroutine(AnimateAtBatResult(outcome));
        }

        private IEnumerator AnimateBatterToPlate()
        {
            if (currentBatterInfo == null || gameManager == null) yield break;

            List<GameObject> lineupCards = GetLineupCardsForTeam(currentBatterInfo.IsAwayTeam);
            int lineupIndex = currentBatterInfo.LineupIndex;
            if (lineupIndex < 0 || lineupIndex >= lineupCards.Count) yield break;

            GameObject sourceCard = lineupCards[lineupIndex];
            if (sourceCard == null) yield break;

            Vector3 startPos = sourceCard.transform.localPosition;
            sourceCard.SetActive(false);

            if (batterCard == null)
            {
                batterCard = CreateFieldCard("BatterCard", startPos, activeBatterColor);
            }
            else
            {
                batterCard.SetActive(true);
            }

            var batter = gameManager.GetCurrentBatter();
            if (batter != null)
            {
                UpdateBatterCardDisplay(batterCard, batter);
            }

            batterCard.transform.localPosition = startPos;
            currentBatterInfo.Card = batterCard;

            yield return StartCoroutine(AnimateCardToPosition(batterCard, homeCardPos));
            RefreshBoard();
        }
        
        public void RefreshBoard()
        {
            if (gameManager == null) return;
            UpdateLineups();
            UpdatePitchers();
            UpdateBatter();
            UpdateBaserunners();
            UpdateScore(gameManager.HomeScore, gameManager.AwayScore);
            UpdateInning(gameManager.CurrentInning, gameManager.IsTopOfInning);
            UpdateOuts(gameManager.Outs);
            UpdateTurnIndicator();
        }
        
        private void UpdateLineups()
        {
            if (gameManager == null) return;
            
            bool userIsAway = IsLocalPlayerAway();
            var topBatters = userIsAway ? gameManager.GetHomeBatters() : gameManager.GetAwayBatters();
            var bottomBatters = userIsAway ? gameManager.GetAwayBatters() : gameManager.GetHomeBatters();

            bool topTeamIsAway = !userIsAway;
            bool bottomTeamIsAway = userIsAway;
            HashSet<int> topPlayersOnField = GetPlayersOnFieldForTeam(topTeamIsAway);
            HashSet<int> bottomPlayersOnField = GetPlayersOnFieldForTeam(bottomTeamIsAway);
            bool topTeamBatting = userIsAway ? !gameManager.IsTopOfInning : gameManager.IsTopOfInning;
            bool bottomTeamBatting = userIsAway ? gameManager.IsTopOfInning : !gameManager.IsTopOfInning;
            
            for (int i = 0; i < 9; i++)
            {
                if (topBatters != null && i < topBatters.Length && topBatters[i] != null)
                {
                    bool isOnField = topPlayersOnField.Contains(i);
                    bool isCurrentBatterOnField = topTeamBatting &&
                        i == gameManager.GetCurrentBatterLineupIndex() &&
                        currentBatterInfo != null;
                    topLineupCards[i].SetActive(!isOnField && !isCurrentBatterOnField);
                    if (!isOnField && !isCurrentBatterOnField)
                    {
                        SetCardText(topLineupCards[i], FormatPlayerName(topBatters[i].PlayerName));
                    }
                }
                if (bottomBatters != null && i < bottomBatters.Length && bottomBatters[i] != null)
                {
                    bool isOnField = bottomPlayersOnField.Contains(i);
                    bool isCurrentBatterOnField = bottomTeamBatting &&
                        i == gameManager.GetCurrentBatterLineupIndex() &&
                        currentBatterInfo != null;
                    bottomLineupCards[i].SetActive(!isOnField && !isCurrentBatterOnField);
                    if (!isOnField && !isCurrentBatterOnField)
                    {
                        SetCardText(bottomLineupCards[i], FormatPlayerName(bottomBatters[i].PlayerName));
                    }
                }
            }
        }
        
        private void UpdatePitchers()
        {
            if (gameManager == null) return;

            if (isAnimatingPitcher) return;

            var pitcher = gameManager.GetCurrentPitcher();
            if (pitcher == null) return;

            bool homePitching = gameManager.IsTopOfInning;
            if (!pitcherOnMound || currentPitcherIsHome != homePitching)
            {
                StartCoroutine(AnimatePitcherChange(homePitching));
                return;
            }

            if (pitcherOnMoundCard != null)
            {
                UpdatePitcherCardDisplay(pitcherOnMoundCard, pitcher);
            }

            bool userIsAway = IsLocalPlayerAway();
            var userPitcher = userIsAway ? gameManager.GetAwayPitcher() : gameManager.GetHomePitcher();
            var opponentPitcher = userIsAway ? gameManager.GetHomePitcher() : gameManager.GetAwayPitcher();
            bool opponentPitching = userIsAway ? homePitching : !homePitching;

            if (userPitcher != null)
            {
                UpdatePitcherCardDisplay(bottomPitcherCard, userPitcher);
                bottomPitcherCard.SetActive(opponentPitching);
            }
            if (opponentPitcher != null)
            {
                UpdatePitcherCardDisplay(topPitcherCard, opponentPitcher);
                topPitcherCard.SetActive(!opponentPitching);
            }
        }
        
        private void UpdateBatter()
        {
            if (gameManager == null || isAnimatingRunners) return;

            var batter = gameManager.GetCurrentBatter();
            if (batter == null) return;

            if (currentBatterInfo != null && currentBatterInfo.Card != null)
            {
                UpdateBatterCardDisplay(currentBatterInfo.Card, batter);
                return;
            }

            if (batterCard == null)
            {
                batterCard = CreateFieldCard("BatterCard", homeCardPos, activeBatterColor);
            }

            batterCard.SetActive(true);
            UpdateBatterCardDisplay(batterCard, batter);
        }

        private void UpdateBaserunners()
        {
            if (gameManager == null || isAnimatingRunners) return;

            if (runnerOnFirst?.Card != null)
            {
                runnerOnFirst.Card.SetActive(true);
                UpdateRunnerCardDisplay(runnerOnFirst.Card, runnerOnFirst);
            }
            if (runnerOnSecond?.Card != null)
            {
                runnerOnSecond.Card.SetActive(true);
                UpdateRunnerCardDisplay(runnerOnSecond.Card, runnerOnSecond);
            }
            if (runnerOnThird?.Card != null)
            {
                runnerOnThird.Card.SetActive(true);
                UpdateRunnerCardDisplay(runnerOnThird.Card, runnerOnThird);
            }
        }

        private void UpdateBatterCardDisplay(GameObject card, BatterCardData batter)
        {
            if (card == null || batter == null) return;
            SetCardText(card, $"{FormatPlayerName(batter.PlayerName)}\nOB {batter.OnBase}\nSPD {batter.Speed}");
        }

        private void UpdateRunnerCardDisplay(GameObject card, RunnerInfo runner)
        {
            if (card == null || runner == null) return;
            SetCardText(card, $"{FormatPlayerName(runner.PlayerName)}\nSPD {runner.Speed}");
        }

        private void UpdatePitcherCardDisplay(GameObject card, PitcherCardData pitcher)
        {
            if (card == null || pitcher == null) return;
            SetCardText(card, $"{FormatPlayerName(pitcher.PlayerName)}\nCTRL {pitcher.Control}");
        }

        private IEnumerator AnimateAtBatResult(AtBatOutcome outcome)
        {
            isAnimatingRunners = true;

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
                    yield return StartCoroutine(AnimateBatterOut());
                    break;
            }

            isAnimatingRunners = false;
            RefreshBoard();
        }

        private IEnumerator AnimateBatterOut()
        {
            RunnerInfo batterToReturn = currentBatterInfo;
            GameObject cardToUse = batterCard;

            if (batterToReturn != null)
            {
                if (batterToReturn.Card == null && cardToUse != null)
                {
                    batterToReturn.Card = cardToUse;
                }

                if (batterToReturn.Card != null)
                {
                    yield return StartCoroutine(AnimateRunnerToLineup(batterToReturn));
                }
                else
                {
                    ReturnRunnerToLineup(batterToReturn);
                }

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
            List<Coroutine> animations = new List<Coroutine>();

            RunnerInfo scoringRunner = runnerOnThird;
            if (scoringRunner != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerScoringFromThird(scoringRunner)));
            }

            RunnerInfo runnerToThird = runnerOnSecond;
            if (runnerToThird != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerToBase(runnerToThird, thirdCardPos)));
            }

            RunnerInfo runnerToSecond = runnerOnFirst;
            if (runnerToSecond != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerToBase(runnerToSecond, secondCardPos)));
            }

            RunnerInfo batterToFirst = currentBatterInfo;
            GameObject batterCardRef = batterCard;
            if (batterToFirst != null && batterToFirst.Card == null && batterCardRef != null)
            {
                batterToFirst.Card = batterCardRef;
            }
            if (batterToFirst != null && batterCardRef != null)
            {
                animations.Add(StartCoroutine(AnimateCardToPosition(batterCardRef, firstCardPos)));
            }

            foreach (var anim in animations)
            {
                yield return anim;
            }

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
            List<Coroutine> animations = new List<Coroutine>();

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

            RunnerInfo runnerToThird = runnerOnFirst;
            if (runnerToThird != null)
            {
                animations.Add(StartCoroutine(AnimateRunnerViaBase(runnerToThird, secondCardPos, thirdCardPos)));
            }

            RunnerInfo batterToSecond = currentBatterInfo;
            GameObject batterCardRef = batterCard;
            if (batterToSecond != null && batterToSecond.Card == null && batterCardRef != null)
            {
                batterToSecond.Card = batterCardRef;
            }
            if (batterToSecond != null && batterCardRef != null)
            {
                animations.Add(StartCoroutine(AnimateBatterViaBase(batterCardRef, firstCardPos, secondCardPos)));
            }

            foreach (var anim in animations)
            {
                yield return anim;
            }

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
            List<Coroutine> animations = new List<Coroutine>();

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

            RunnerInfo batterToThird = currentBatterInfo;
            GameObject batterCardRef = batterCard;
            if (batterToThird != null && batterToThird.Card == null && batterCardRef != null)
            {
                batterToThird.Card = batterCardRef;
            }
            if (batterToThird != null && batterCardRef != null)
            {
                animations.Add(StartCoroutine(AnimateBatterViaBases(batterCardRef, firstCardPos, secondCardPos, thirdCardPos)));
            }

            foreach (var anim in animations)
            {
                yield return anim;
            }

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
            List<Coroutine> animations = new List<Coroutine>();

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

            RunnerInfo batterScoring = currentBatterInfo;
            GameObject batterCardRef = batterCard;
            if (batterScoring != null && batterScoring.Card == null && batterCardRef != null)
            {
                batterScoring.Card = batterCardRef;
            }
            if (batterScoring != null && batterCardRef != null)
            {
                animations.Add(StartCoroutine(AnimateBatterHomeRun(batterScoring, batterCardRef)));
            }

            foreach (var anim in animations)
            {
                yield return anim;
            }

            runnerOnThird = null;
            runnerOnSecond = null;
            runnerOnFirst = null;
            currentBatterInfo = null;
            batterCard = null;
        }

        private IEnumerator AnimateRunnerViaBase(RunnerInfo runner, Vector3 viaPos, Vector3 endPos)
        {
            if (runner?.Card == null) yield break;
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, viaPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, endPos));
        }

        private IEnumerator AnimateBatterViaBase(GameObject card, Vector3 viaPos, Vector3 endPos)
        {
            if (card == null) yield break;
            yield return StartCoroutine(AnimateCardToPosition(card, viaPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(card, endPos));
        }

        private IEnumerator AnimateBatterViaBases(GameObject card, Vector3 pos1, Vector3 pos2, Vector3 pos3)
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
            yield return StartCoroutine(AnimateCardToPosition(card, firstCardPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(card, secondCardPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(card, thirdCardPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(card, homeCardPos));
            yield return new WaitForSeconds(0.15f);
            yield return StartCoroutine(AnimateRunnerToLineup(batter));
        }

        private IEnumerator AnimateRunnerToBase(RunnerInfo runner, Vector3 targetPos)
        {
            if (runner?.Card == null) yield break;
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, targetPos));
        }

        private IEnumerator AnimateRunnerScoringFromThird(RunnerInfo runner)
        {
            if (runner?.Card == null) yield break;
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, homeCardPos));
            yield return new WaitForSeconds(0.15f);
            yield return StartCoroutine(AnimateRunnerToLineup(runner));
        }

        private IEnumerator AnimateRunnerScoringFromSecond(RunnerInfo runner)
        {
            if (runner?.Card == null) yield break;
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, thirdCardPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, homeCardPos));
            yield return new WaitForSeconds(0.15f);
            yield return StartCoroutine(AnimateRunnerToLineup(runner));
        }

        private IEnumerator AnimateRunnerScoringFromFirst(RunnerInfo runner)
        {
            if (runner?.Card == null) yield break;
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, secondCardPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, thirdCardPos));
            yield return new WaitForSeconds(baseRunningDelay);
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, homeCardPos));
            yield return new WaitForSeconds(0.15f);
            yield return StartCoroutine(AnimateRunnerToLineup(runner));
        }

        private IEnumerator AnimateCardToPosition(GameObject card, Vector3 targetPos)
        {
            if (card == null) yield break;
            Vector3 startPos = card.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
                card.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            card.transform.localPosition = targetPos;
        }

        private void ReturnRunnerToLineup(RunnerInfo runner)
        {
            if (runner == null) return;

            GetPlayersOnFieldForTeam(runner.IsAwayTeam).Remove(runner.LineupIndex);
            List<GameObject> lineupCards = GetLineupCardsForTeam(runner.IsAwayTeam);
            if (runner.LineupIndex >= 0 && runner.LineupIndex < lineupCards.Count)
            {
                if (lineupCards[runner.LineupIndex] != null)
                {
                    lineupCards[runner.LineupIndex].SetActive(true);
                }
            }

            if (runner.Card != null)
            {
                runner.Card.SetActive(false);
            }
        }

        private IEnumerator AnimateRunnerToLineup(RunnerInfo runner)
        {
            if (runner == null || runner.Card == null) yield break;

            List<GameObject> lineupCards = GetLineupCardsForTeam(runner.IsAwayTeam);
            if (runner.LineupIndex < 0 || runner.LineupIndex >= lineupCards.Count) yield break;

            GameObject targetCard = lineupCards[runner.LineupIndex];
            if (targetCard == null) yield break;

            Vector3 targetPos = targetCard.transform.localPosition;
            yield return StartCoroutine(AnimateCardToPosition(runner.Card, targetPos));
            ReturnRunnerToLineup(runner);
        }

        private IEnumerator AnimateInningChange()
        {
            isAnimatingRunners = true;

            RunnerInfo savedRunner3 = runnerOnThird;
            RunnerInfo savedRunner2 = runnerOnSecond;
            RunnerInfo savedRunner1 = runnerOnFirst;
            RunnerInfo savedBatter = currentBatterInfo;
            GameObject savedBatterCard = batterCard;

            runnerOnFirst = null;
            runnerOnSecond = null;
            runnerOnThird = null;
            currentBatterInfo = null;
            batterCard = null;

            List<RunnerInfo> runnersToReturn = new List<RunnerInfo>();
            if (savedRunner3 != null) runnersToReturn.Add(savedRunner3);
            if (savedRunner2 != null) runnersToReturn.Add(savedRunner2);
            if (savedRunner1 != null) runnersToReturn.Add(savedRunner1);
            if (savedBatter != null) runnersToReturn.Add(savedBatter);

            List<Coroutine> animations = new List<Coroutine>();
            foreach (var runner in runnersToReturn)
            {
                if (runner?.Card != null)
                {
                    animations.Add(StartCoroutine(AnimateRunnerToLineup(runner)));
                }
            }

            foreach (var anim in animations)
            {
                yield return anim;
            }

            awayPlayersOnField.Clear();
            homePlayersOnField.Clear();

            if (savedBatterCard != null)
            {
                savedBatterCard.SetActive(false);
            }

            isAnimatingRunners = false;
            RefreshBoard();
        }

        private IEnumerator AnimatePitcherChange(bool newPitcherIsHome)
        {
            isAnimatingPitcher = true;

            if (pitcherOnMound && pitcherOnMoundCard != null)
            {
                yield return StartCoroutine(AnimatePitcherToLineup(currentPitcherIsHome));
            }

            yield return StartCoroutine(AnimatePitcherToMound(newPitcherIsHome));

            pitcherOnMound = true;
            currentPitcherIsHome = newPitcherIsHome;

            bool userIsAway = IsLocalPlayerAway();
            var userPitcher = userIsAway ? gameManager.GetAwayPitcher() : gameManager.GetHomePitcher();
            var opponentPitcher = userIsAway ? gameManager.GetHomePitcher() : gameManager.GetAwayPitcher();
            bool opponentPitching = userIsAway ? newPitcherIsHome : !newPitcherIsHome;

            if (userPitcher != null)
            {
                UpdatePitcherCardDisplay(bottomPitcherCard, userPitcher);
                bottomPitcherCard.SetActive(opponentPitching);
            }
            if (opponentPitcher != null)
            {
                UpdatePitcherCardDisplay(topPitcherCard, opponentPitcher);
                topPitcherCard.SetActive(!opponentPitching);
            }

            isAnimatingPitcher = false;
        }

        private IEnumerator AnimatePitcherToMound(bool isHomePitcher)
        {
            var pitcher = isHomePitcher ? gameManager.GetHomePitcher() : gameManager.GetAwayPitcher();
            if (pitcher == null) yield break;

            GameObject sourcePitcherCard = GetPitcherCardForTeam(isHomePitcher);
            if (sourcePitcherCard == null) yield break;

            if (pitcherOnMoundCard == null)
            {
                pitcherOnMoundCard = CreateFieldCard("PitcherOnMound", pitcherMoundPos, activeBatterColor);
            }

            Vector3 startPos = sourcePitcherCard.transform.localPosition;
            sourcePitcherCard.SetActive(false);
            UpdatePitcherCardDisplay(pitcherOnMoundCard, pitcher);
            pitcherOnMoundCard.SetActive(true);
            pitcherOnMoundCard.transform.localPosition = startPos;

            yield return StartCoroutine(AnimateCardToPosition(pitcherOnMoundCard, pitcherMoundPos));
        }

        private IEnumerator AnimatePitcherToLineup(bool isHomePitcher)
        {
            if (pitcherOnMoundCard == null) yield break;

            GameObject targetPitcherCard = GetPitcherCardForTeam(isHomePitcher);
            if (targetPitcherCard == null) yield break;

            Vector3 targetPos = targetPitcherCard.transform.localPosition;
            yield return StartCoroutine(AnimateCardToPosition(pitcherOnMoundCard, targetPos));
            pitcherOnMoundCard.SetActive(false);
            targetPitcherCard.SetActive(true);
        }
        
        private void SetCardText(GameObject card, string text)
        {
            if (card == null) return;
            var tmp = card.GetComponentInChildren<TextMeshPro>();
            if (tmp != null) tmp.text = text;
        }

        private string FormatPlayerName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "";
            string[] parts = fullName.Trim().Split(' ');
            return parts.Length > 1 ? parts[parts.Length - 1] : fullName;
        }
        
        private void UpdateScore(int home, int away)
        {
            if (scoreText == null) return;
            bool userIsAway = IsLocalPlayerAway();
            int userScore = userIsAway ? away : home;
            int oppScore = userIsAway ? home : away;
            scoreText.text = $"YOU {userScore}   OPP {oppScore}";
        }
        
        private void UpdateInning(int inning, bool isTop)
        {
            if (inningText != null)
                inningText.text = $"{(isTop ? "TOP" : "BOT")} {inning}";
        }
        
        private void UpdateOuts(int outs)
        {
            if (outsText != null)
            {
                outsText.text = $"{outs} OUT{(outs != 1 ? "S" : "")}";
                outsText.color = outs >= 2 ? new Color(1f, 0.4f, 0.4f) : Color.white;
            }
        }
        
        private void UpdateTurnIndicator()
        {
            if (turnText == null || gameManager == null) return;
            
            bool isPlayerTurn = gameManager.IsLocalPlayerTurn();
            turnText.text = isPlayerTurn ? "YOUR TURN - ROLL" : "OPPONENT'S TURN";
            turnText.color = isPlayerTurn ? Color.yellow : Color.gray;
        }
        
        private void ShowMessage(string msg)
        {
            if (messageText != null)
            {
                if (string.IsNullOrWhiteSpace(msg))
                {
                    messageText.text = "";
                }
                else
                {
                    string firstLine = msg.Split('\n')[0];
                    messageText.text = firstLine.Length > 48 ? firstLine.Substring(0, 48) + "..." : firstLine;
                }
                StartCoroutine(ClearMessage());
            }
        }
        
        private IEnumerator ClearMessage()
        {
            yield return new WaitForSeconds(3f);
            if (messageText != null) messageText.text = "";
        }
        
        private bool IsLocalPlayerAway()
        {
            if (gameManager == null) return false;
            if (gameManager.IsCPUGame) return gameManager.CPUIsHome;
            return !gameManager.IsLocalPlayerHome();
        }

        private List<GameObject> GetLineupCardsForTeam(bool isAwayTeam)
        {
            bool userIsAway = IsLocalPlayerAway();
            if (userIsAway)
            {
                return isAwayTeam ? bottomLineupCards : topLineupCards;
            }
            return isAwayTeam ? topLineupCards : bottomLineupCards;
        }

        private GameObject GetPitcherCardForTeam(bool isHomeTeam)
        {
            bool userIsAway = IsLocalPlayerAway();
            if (userIsAway)
            {
                return isHomeTeam ? topPitcherCard : bottomPitcherCard;
            }
            return isHomeTeam ? bottomPitcherCard : topPitcherCard;
        }

        private HashSet<int> GetPlayersOnFieldForTeam(bool isAwayTeam)
        {
            return isAwayTeam ? awayPlayersOnField : homePlayersOnField;
        }
        
        void Update()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !isRollingDice)
            {
                if (gameManager != null && gameManager.IsLocalPlayerTurn())
                {
                    RollDice();
                    gameManager.RequestDiceRoll();
                }
            }
        }

        public void RollDice()
        {
            if (isRollingDice) return;
            StartCoroutine(PerformDiceRoll());
        }
        
        private IEnumerator PerformDiceRoll()
        {
            isRollingDice = true;
            
            if (currentDice != null) Destroy(currentDice);
            
            currentDice = CreateD20();
            currentDice.transform.SetParent(boardRoot.transform);
            currentDice.transform.localPosition = new Vector3(Random.Range(-1.2f, 1.2f), 4f, Random.Range(-0.8f, 0.8f));
            currentDice.transform.localRotation = Random.rotation;
            
            diceRigidbody = currentDice.GetComponent<Rigidbody>();
            Vector3 throwDir = new Vector3(Random.Range(-0.3f, 0.3f), -1f, Random.Range(-0.3f, 0.3f)).normalized;
            diceRigidbody.AddForce(throwDir * diceThrowForce, ForceMode.Impulse);
            diceRigidbody.AddTorque(Random.insideUnitSphere * diceTorque, ForceMode.Impulse);
            
            float minRollTime = 0.8f;
            float maxRollTime = 3.6f;
            float settleTime = 0.25f;
            float settleTimer = 0f;
            float elapsed = 0f;
            float velocityThreshold = 0.06f;
            float angularThreshold = 0.2f;
            float velocityThresholdSqr = velocityThreshold * velocityThreshold;
            float angularThresholdSqr = angularThreshold * angularThreshold;

            while (elapsed < maxRollTime)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= minRollTime && diceRigidbody != null)
                {
                    bool slowEnough = diceRigidbody.linearVelocity.sqrMagnitude < velocityThresholdSqr &&
                        diceRigidbody.angularVelocity.sqrMagnitude < angularThresholdSqr;
                    if (slowEnough)
                    {
                        settleTimer += Time.deltaTime;
                        if (settleTimer >= settleTime)
                        {
                            break;
                        }
                    }
                    else
                    {
                        settleTimer = 0f;
                    }
                }
                yield return null;
            }

            if (diceRigidbody != null)
            {
                diceRigidbody.linearVelocity = Vector3.zero;
                diceRigidbody.angularVelocity = Vector3.zero;
                diceRigidbody.Sleep();
                diceRigidbody.isKinematic = true;
            }

            TryApplyDiceResult();
            if (currentDice != null && pendingDiceResult == null)
            {
                SnapDiceToTopFace();
            }
            
            yield return new WaitForSeconds(1f);
            
            if (currentDice != null) { Destroy(currentDice); currentDice = null; }
            isRollingDice = false;
        }
        
        private GameObject CreateD20()
        {
            GameObject dice = new GameObject("D20");
            MeshFilter mf = dice.AddComponent<MeshFilter>();
            MeshRenderer mr = dice.AddComponent<MeshRenderer>();
            mf.mesh = CreateIcosahedronMesh();
            
            Material diceMat = new Material(Shader.Find("Unlit/Color"));
            if (diceMat.shader == null) diceMat = new Material(Shader.Find("UI/Default"));
            diceMat.color = new Color(0.85f, 0.1f, 0.1f);
            mr.material = diceMat;
            
            dice.transform.localScale = Vector3.one * diceScale;
            
            MeshCollider mc = dice.AddComponent<MeshCollider>();
            mc.convex = true;
            mc.sharedMesh = mf.mesh;
            
            PhysicsMaterial pm = new PhysicsMaterial();
            pm.bounciness = 0.25f;
            pm.dynamicFriction = 0.7f;
            pm.staticFriction = 0.7f;
            pm.frictionCombine = PhysicsMaterialCombine.Average;
            pm.bounceCombine = PhysicsMaterialCombine.Average;
            mc.material = pm;
            
            Rigidbody rb = dice.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0.6f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.maxAngularVelocity = 12f;
            rb.sleepThreshold = 0.02f;
            
            AddFaceNumbers(dice, mf.mesh);
            AddEdgeOverlay(dice, mf.mesh);

            return dice;
        }
        
        private Mesh CreateIcosahedronMesh()
        {
            Mesh mesh = new Mesh();
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            
            Vector3[] bv = new Vector3[] {
                new Vector3(-1, t, 0).normalized, new Vector3(1, t, 0).normalized,
                new Vector3(-1, -t, 0).normalized, new Vector3(1, -t, 0).normalized,
                new Vector3(0, -1, t).normalized, new Vector3(0, 1, t).normalized,
                new Vector3(0, -1, -t).normalized, new Vector3(0, 1, -t).normalized,
                new Vector3(t, 0, -1).normalized, new Vector3(t, 0, 1).normalized,
                new Vector3(-t, 0, -1).normalized, new Vector3(-t, 0, 1).normalized
            };
            
            int[] ti = new int[] {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            
            Vector3[] v = new Vector3[ti.Length];
            int[] tr = new int[ti.Length];
            for (int i = 0; i < ti.Length; i++) { v[i] = bv[ti[i]]; tr[i] = i; }
            
            mesh.vertices = v;
            mesh.triangles = tr;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddFaceNumbers(GameObject dice, Mesh mesh)
        {
            if (dice == null || mesh == null) return;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            int faceCount = triangles.Length / 3;

            for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
            {
                int i0 = triangles[faceIndex * 3];
                int i1 = triangles[faceIndex * 3 + 1];
                int i2 = triangles[faceIndex * 3 + 2];

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];

                Vector3 center = (v0 + v1 + v2) / 3f;
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                GameObject faceText = new GameObject($"Face_{faceIndex + 1}");
                faceText.transform.SetParent(dice.transform, false);
                faceText.transform.localPosition = center + normal * 0.06f;
                faceText.transform.localRotation = Quaternion.LookRotation(normal);
                faceText.transform.localScale = Vector3.one * 0.28f;

                TextMeshPro tmp = faceText.AddComponent<TextMeshPro>();
                tmp.text = (faceIndex + 1).ToString();
                tmp.fontSize = 10f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.fontStyle = FontStyles.Bold;
                tmp.enableWordWrapping = false;
                tmp.outlineColor = Color.black;
                tmp.outlineWidth = 0.2f;
            }
        }

        private struct Edge : System.IEquatable<Edge>
        {
            public int A;
            public int B;

            public Edge(int a, int b)
            {
                A = Mathf.Min(a, b);
                B = Mathf.Max(a, b);
            }

            public bool Equals(Edge other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is Edge other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A * 397) ^ B;
                }
            }
        }

        private void AddEdgeOverlay(GameObject dice, Mesh mesh)
        {
            if (dice == null || mesh == null) return;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            HashSet<Edge> edges = new HashSet<Edge>();

            for (int i = 0; i < triangles.Length; i += 3)
            {
                edges.Add(new Edge(triangles[i], triangles[i + 1]));
                edges.Add(new Edge(triangles[i + 1], triangles[i + 2]));
                edges.Add(new Edge(triangles[i + 2], triangles[i]));
            }

            List<Vector3> lineVertices = new List<Vector3>(edges.Count * 2);
            foreach (Edge edge in edges)
            {
                lineVertices.Add(vertices[edge.A]);
                lineVertices.Add(vertices[edge.B]);
            }

            Mesh lineMesh = new Mesh();
            lineMesh.name = "DiceEdges";
            lineMesh.SetVertices(lineVertices);

            int[] indices = new int[lineVertices.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            lineMesh.SetIndices(indices, MeshTopology.Lines, 0);
            lineMesh.RecalculateBounds();

            GameObject edgeObj = new GameObject("DiceEdges");
            edgeObj.transform.SetParent(dice.transform, false);
            edgeObj.transform.localScale = Vector3.one * 1.01f;

            MeshFilter edgeFilter = edgeObj.AddComponent<MeshFilter>();
            edgeFilter.mesh = lineMesh;

            MeshRenderer edgeRenderer = edgeObj.AddComponent<MeshRenderer>();
            Material lineMat = new Material(Shader.Find("Unlit/Color"));
            if (lineMat.shader == null) lineMat = new Material(Shader.Find("UI/Default"));
            lineMat.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            edgeRenderer.material = lineMat;
        }

        private void TryApplyDiceResult()
        {
            if (pendingDiceResult == null) return;
            if (currentDice == null) return;
            if (diceRigidbody != null && !diceRigidbody.isKinematic) return;

            SnapDiceToResult(pendingDiceResult.Value);
            pendingDiceResult = null;
        }

        private void SnapDiceToResult(int result)
        {
            if (currentDice == null) return;
            MeshFilter filter = currentDice.GetComponent<MeshFilter>();
            if (filter == null || filter.mesh == null) return;

            int faceCount = filter.mesh.triangles.Length / 3;
            if (faceCount <= 0) return;

            int faceIndex = Mathf.Clamp(result - 1, 0, faceCount - 1);
            AlignDiceToFace(filter.mesh, faceIndex);
        }

        private void SnapDiceToTopFace()
        {
            if (currentDice == null) return;
            MeshFilter filter = currentDice.GetComponent<MeshFilter>();
            if (filter == null || filter.mesh == null) return;

            int[] triangles = filter.mesh.triangles;
            Vector3[] vertices = filter.mesh.vertices;
            int faceCount = triangles.Length / 3;
            if (faceCount <= 0) return;

            int bestFace = 0;
            float bestDot = -1f;
            for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
            {
                Vector3 normal = GetFaceNormal(vertices, triangles, faceIndex);
                Vector3 worldNormal = currentDice.transform.TransformDirection(normal);
                float dot = Vector3.Dot(worldNormal, Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestFace = faceIndex;
                }
            }

            AlignDiceToFace(filter.mesh, bestFace);
        }

        private void AlignDiceToFace(Mesh mesh, int faceIndex)
        {
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            Vector3 localNormal = GetFaceNormal(vertices, triangles, faceIndex);
            Vector3 worldNormal = currentDice.transform.TransformDirection(localNormal);
            Quaternion align = Quaternion.FromToRotation(worldNormal, Vector3.up);
            currentDice.transform.rotation = align * currentDice.transform.rotation;
        }

        private Vector3 GetFaceNormal(Vector3[] vertices, int[] triangles, int faceIndex)
        {
            int i0 = triangles[faceIndex * 3];
            int i1 = triangles[faceIndex * 3 + 1];
            int i2 = triangles[faceIndex * 3 + 2];

            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            return Vector3.Cross(v1 - v0, v2 - v0).normalized;
        }
        
        void OnDestroy()
        {
            if (diceRoller != null)
            {
                diceRoller.OnDiceRollStarted -= HandleDiceRollStarted;
                diceRoller.OnDiceRollComplete -= HandleDiceRollComplete;
            }
            if (currentDice != null) Destroy(currentDice);
            if (boardRoot != null) Destroy(boardRoot);
        }
    }
}
