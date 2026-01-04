using System.Collections;
using UnityEngine;
using MLBShowdown.Network;
using MLBShowdown.BaseRunning;

namespace MLBShowdown.Core
{
    public class CPUPlayer : MonoBehaviour
    {
        [Header("AI Settings")]
        [SerializeField] private float decisionDelay = 1.5f;
        [SerializeField] private float rollDelay = 2f;

        [Header("Strategy Thresholds")]
        [SerializeField] private int minSpeedForSteal = 14;
        [SerializeField] private float stealAttemptChance = 0.6f;
        [SerializeField] private float tagUpChance = 0.8f;
        [SerializeField] private float doublePlayAttemptChance = 0.9f;

        private NetworkGameManager gameManager;
        private bool isProcessing;

        void Start()
        {
            StartCoroutine(WaitForGameManager());
        }

        private IEnumerator WaitForGameManager()
        {
            yield return new WaitUntil(() => NetworkGameManager.Instance != null);
            gameManager = NetworkGameManager.Instance;
            gameManager.OnGameStateChanged += HandleStateChanged;
        }

        void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(GameState newState)
        {
            if (!gameManager.IsCPUGame || isProcessing) return;

            // Check if it's CPU's turn
            if (IsCPUTurn(newState))
            {
                StartCoroutine(ProcessCPUTurn(newState));
            }
        }

        private bool IsCPUTurn(GameState state)
        {
            bool cpuIsHome = gameManager.CPUIsHome;
            bool isTopOfInning = gameManager.IsTopOfInning;

            switch (state)
            {
                case GameState.DefenseTurn:
                    // CPU defends when: CPU is home and top of inning, or CPU is away and bottom
                    return (cpuIsHome && isTopOfInning) || (!cpuIsHome && !isTopOfInning);

                case GameState.OffenseTurn:
                    // CPU bats when: CPU is home and bottom of inning, or CPU is away and top
                    return (cpuIsHome && !isTopOfInning) || (!cpuIsHome && isTopOfInning);

                case GameState.OptionalAction:
                    return IsCPUOptionalActionTurn();

                case GameState.RollForTeamAssignment:
                    return true; // CPU always participates in team roll

                default:
                    return false;
            }
        }

        private bool IsCPUOptionalActionTurn()
        {
            var actionType = gameManager.AvailableOptionalAction;
            bool isOffensiveAction = actionType == OptionalActionType.StolenBase || 
                                    actionType == OptionalActionType.TagUp;
            
            bool cpuIsHome = gameManager.CPUIsHome;
            bool isTopOfInning = gameManager.IsTopOfInning;

            if (isOffensiveAction)
            {
                // Offensive player decides
                return (cpuIsHome && !isTopOfInning) || (!cpuIsHome && isTopOfInning);
            }
            else
            {
                // Defensive player decides (double play)
                return (cpuIsHome && isTopOfInning) || (!cpuIsHome && !isTopOfInning);
            }
        }

        private IEnumerator ProcessCPUTurn(GameState state)
        {
            isProcessing = true;
            yield return new WaitForSeconds(decisionDelay);

            switch (state)
            {
                case GameState.RollForTeamAssignment:
                case GameState.DefenseTurn:
                case GameState.OffenseTurn:
                    yield return new WaitForSeconds(rollDelay - decisionDelay);
                    PerformRoll();
                    break;

                case GameState.OptionalAction:
                    yield return ProcessOptionalAction();
                    break;
            }

            isProcessing = false;
        }

        private void PerformRoll()
        {
            // The game manager handles the actual roll
            // We just need to trigger it
            Debug.Log("[CPU] Rolling dice...");
        }

        private IEnumerator ProcessOptionalAction()
        {
            var actionType = gameManager.AvailableOptionalAction;
            bool shouldAttempt = EvaluateOptionalAction(actionType);

            yield return new WaitForSeconds(0.5f);

            if (shouldAttempt)
            {
                Debug.Log($"[CPU] Attempting {actionType}");
                gameManager.RPC_AttemptOptionalAction();
                yield return new WaitForSeconds(rollDelay);
                PerformRoll();
            }
            else
            {
                Debug.Log($"[CPU] Declining {actionType}");
                gameManager.RPC_DeclineOptionalAction();
            }
        }

        private bool EvaluateOptionalAction(OptionalActionType actionType)
        {
            switch (actionType)
            {
                case OptionalActionType.StolenBase:
                    return EvaluateStealAttempt();

                case OptionalActionType.TagUp:
                    return EvaluateTagUp();

                case OptionalActionType.DoublePlay:
                    return EvaluateDoublePlay();

                default:
                    return false;
            }
        }

        private bool EvaluateStealAttempt()
        {
            // Get runner speed from base runner controller
            var baseRunnerController = FindObjectOfType<BaseRunnerController>();
            if (baseRunnerController == null) return false;

            // Check runner on second first (more valuable steal)
            var runnerOnSecond = baseRunnerController.GetRunnerOn(Base.Second);
            if (runnerOnSecond.HasValue && runnerOnSecond.Value.Speed >= minSpeedForSteal)
            {
                return Random.value < stealAttemptChance;
            }

            // Check runner on first
            var runnerOnFirst = baseRunnerController.GetRunnerOn(Base.First);
            if (runnerOnFirst.HasValue && runnerOnFirst.Value.Speed >= minSpeedForSteal)
            {
                return Random.value < stealAttemptChance;
            }

            return false;
        }

        private bool EvaluateTagUp()
        {
            // Usually attempt tag up with runner on third - it's a free run attempt
            // Consider game situation
            int outs = gameManager.Outs;
            
            // More likely to attempt with 2 outs (last chance)
            float adjustedChance = outs == 2 ? tagUpChance + 0.15f : tagUpChance;
            
            return Random.value < adjustedChance;
        }

        private bool EvaluateDoublePlay()
        {
            // Defense almost always attempts double play
            return Random.value < doublePlayAttemptChance;
        }

        // Advanced AI considerations
        private float GetGameSituationModifier()
        {
            int homeScore = gameManager.HomeScore;
            int awayScore = gameManager.AwayScore;
            int inning = gameManager.CurrentInning;
            bool cpuIsHome = gameManager.CPUIsHome;

            int cpuScore = cpuIsHome ? homeScore : awayScore;
            int oppScore = cpuIsHome ? awayScore : homeScore;
            int scoreDiff = cpuScore - oppScore;

            // More aggressive when behind in late innings
            if (inning >= 7 && scoreDiff < 0)
            {
                return 0.2f; // Increase attempt chances
            }

            // More conservative with a lead in late innings
            if (inning >= 7 && scoreDiff > 0)
            {
                return -0.15f; // Decrease attempt chances
            }

            return 0f;
        }
    }
}
