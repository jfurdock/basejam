using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using MLBShowdown.Core;
using MLBShowdown.Cards;
using MLBShowdown.BaseRunning;
using MLBShowdown.Dice;

namespace MLBShowdown.Network
{
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private DiceRoller3D diceRoller;
        [SerializeField] private BaseRunnerController baseRunnerController;

        [Header("Game Settings")]
        [SerializeField] private int totalInnings = 9;

        // Flag to track if we're in single player (non-networked) mode
        private bool isLocalMode = false;

        // Local backing fields for single player mode
        private GameState _localCurrentState;
        private int _localCurrentInning;
        private bool _localIsTopOfInning;
        private int _localOuts;
        private int _localHomeScore;
        private int _localAwayScore;
        private int _localCurrentBatterIndex;
        private bool _localBatterHasAdvantage;
        private int _localDefenseRollResult;
        private int _localOffenseRollResult;
        private AtBatOutcome _localCurrentOutcome;
        private OptionalActionType _localAvailableOptionalAction;
        private bool _localIsCPUGame;
        private bool _localCPUIsHome;
        private int _localHomePitcherIndex;
        private int _localAwayPitcherIndex;
        private int _localHomeBatterUp;
        private int _localAwayBatterUp;
        private int[] _localHomeBatterIndices = new int[9];
        private int[] _localAwayBatterIndices = new int[9];
        private int _localHomeInfieldDefense;
        private int _localHomeOutfieldDefense;
        private int _localHomeCatcherDefense;
        private int _localAwayInfieldDefense;
        private int _localAwayOutfieldDefense;
        private int _localAwayCatcherDefense;
        private int _localHomeTeamRoll;
        private int _localAwayTeamRoll;

        // Networked Game State (only used when properly spawned)
        [Networked] private GameState _netCurrentState { get; set; }
        [Networked] private int _netCurrentInning { get; set; }
        [Networked] private NetworkBool _netIsTopOfInning { get; set; }
        [Networked] private int _netOuts { get; set; }
        [Networked] private int _netHomeScore { get; set; }
        [Networked] private int _netAwayScore { get; set; }

        // Public properties that use local or networked based on mode
        public GameState CurrentState 
        { 
            get => isLocalMode ? _localCurrentState : _netCurrentState;
            set { if (isLocalMode) _localCurrentState = value; else _netCurrentState = value; }
        }
        public int CurrentInning 
        { 
            get => isLocalMode ? _localCurrentInning : _netCurrentInning;
            set { if (isLocalMode) _localCurrentInning = value; else _netCurrentInning = value; }
        }
        public bool IsTopOfInning 
        { 
            get => isLocalMode ? _localIsTopOfInning : _netIsTopOfInning;
            set { if (isLocalMode) _localIsTopOfInning = value; else _netIsTopOfInning = value; }
        }
        public int Outs 
        { 
            get => isLocalMode ? _localOuts : _netOuts;
            set { if (isLocalMode) _localOuts = value; else _netOuts = value; }
        }
        public int HomeScore 
        { 
            get => isLocalMode ? _localHomeScore : _netHomeScore;
            set { if (isLocalMode) _localHomeScore = value; else _netHomeScore = value; }
        }
        public int AwayScore 
        { 
            get => isLocalMode ? _localAwayScore : _netAwayScore;
            set { if (isLocalMode) _localAwayScore = value; else _netAwayScore = value; }
        }

        // Player assignments
        [Networked] public PlayerRef HomePlayer { get; set; }
        [Networked] public PlayerRef AwayPlayer { get; set; }
        
        /// <summary>
        /// Returns true if the local player is the one who should act in the current game state.
        /// In multiplayer: Defense rolls when their team is pitching, Offense rolls when their team is batting.
        /// </summary>
        public bool IsLocalPlayerTurn()
        {
            // For CPU games, use the CPU logic
            if (_localIsCPUGame) return !ShouldCPURoll();
            if (isLocalMode) return true; // Local mode, always player's turn
            
            // Use Runner.LocalPlayer directly for most accurate value
            var runner = NetworkRunnerHandler.Instance?.Runner;
            if (runner == null)
            {
                Debug.LogWarning("[IsLocalPlayerTurn] Runner is null!");
                return false;
            }
            
            var localPlayer = runner.LocalPlayer;
            if (localPlayer == default) 
            {
                Debug.LogWarning("[IsLocalPlayerTurn] LocalPlayer is default/null!");
                return false;
            }
            
            // Ensure we're reading from a valid networked object
            if (Object == null || !Object.IsValid)
            {
                Debug.LogWarning("[IsLocalPlayerTurn] NetworkObject is null or invalid!");
                return false;
            }
            
            // In Fusion, the host is typically PlayerRef with lower ID
            // Host = Home team, Client = Away team
            bool isHost = runner.IsServer;
            bool isLocalPlayerHome = isHost;
            bool isLocalPlayerAway = !isHost;
            
            Debug.Log($"[IsLocalPlayerTurn] localPlayer={localPlayer}, isHost={isHost}, HomePlayer={HomePlayer}, AwayPlayer={AwayPlayer}");
            Debug.Log($"[IsLocalPlayerTurn] isLocalPlayerHome={isLocalPlayerHome}, isLocalPlayerAway={isLocalPlayerAway}, IsTopOfInning={IsTopOfInning}");
            
            // Determine who should act based on game state and inning half
            switch (CurrentState)
            {
                case GameState.DefenseTurn:
                    // Defense (pitching team) rolls
                    // Top of inning = Away batting, Home pitching
                    // Bottom of inning = Home batting, Away pitching
                    if (IsTopOfInning)
                        return isLocalPlayerHome; // Home is pitching (defense)
                    else
                        return isLocalPlayerAway; // Away is pitching (defense)
                    
                case GameState.OffenseTurn:
                    // Offense (batting team) rolls
                    if (IsTopOfInning)
                        return isLocalPlayerAway; // Away is batting (offense)
                    else
                        return isLocalPlayerHome; // Home is batting (offense)
                    
                case GameState.OptionalAction:
                    // Batting team decides optional actions
                    if (IsTopOfInning)
                        return isLocalPlayerAway;
                    else
                        return isLocalPlayerHome;
                        
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Returns the PlayerRef of who should act in the current state.
        /// </summary>
        public PlayerRef GetCurrentActingPlayer()
        {
            if (IsCPUGame) return default; // CPU game doesn't use PlayerRef
            
            switch (CurrentState)
            {
                case GameState.DefenseTurn:
                    return IsTopOfInning ? HomePlayer : AwayPlayer;
                    
                case GameState.OffenseTurn:
                case GameState.OptionalAction:
                    return IsTopOfInning ? AwayPlayer : HomePlayer;
                    
                default:
                    return default;
            }
        }
        
        /// <summary>
        /// Returns true if the local player is the Home team.
        /// </summary>
        public bool IsLocalPlayerHome()
        {
            if (isLocalMode || _localIsCPUGame) return !_localCPUIsHome; // In CPU game, player is opposite of CPU
            
            // In multiplayer, host is Home team
            var runner = NetworkRunnerHandler.Instance?.Runner;
            return runner != null && runner.IsServer;
        }
        
        public int HomeTeamRoll 
        { 
            get => isLocalMode ? _localHomeTeamRoll : _netHomeTeamRoll;
            set { if (isLocalMode) _localHomeTeamRoll = value; else _netHomeTeamRoll = value; }
        }
        [Networked] private int _netHomeTeamRoll { get; set; }
        
        public int AwayTeamRoll 
        { 
            get => isLocalMode ? _localAwayTeamRoll : _netAwayTeamRoll;
            set { if (isLocalMode) _localAwayTeamRoll = value; else _netAwayTeamRoll = value; }
        }
        [Networked] private int _netAwayTeamRoll { get; set; }

        // Current at-bat state
        public int CurrentBatterIndex 
        { 
            get => isLocalMode ? _localCurrentBatterIndex : _netCurrentBatterIndex;
            set { if (isLocalMode) _localCurrentBatterIndex = value; else _netCurrentBatterIndex = value; }
        }
        [Networked] private int _netCurrentBatterIndex { get; set; }
        
        public bool BatterHasAdvantage 
        { 
            get => isLocalMode ? _localBatterHasAdvantage : _netBatterHasAdvantage;
            set { if (isLocalMode) _localBatterHasAdvantage = value; else _netBatterHasAdvantage = value; }
        }
        [Networked] private NetworkBool _netBatterHasAdvantage { get; set; }
        
        public int DefenseRollResult 
        { 
            get => isLocalMode ? _localDefenseRollResult : _netDefenseRollResult;
            set { if (isLocalMode) _localDefenseRollResult = value; else _netDefenseRollResult = value; }
        }
        [Networked] private int _netDefenseRollResult { get; set; }
        
        public int OffenseRollResult 
        { 
            get => isLocalMode ? _localOffenseRollResult : _netOffenseRollResult;
            set { if (isLocalMode) _localOffenseRollResult = value; else _netOffenseRollResult = value; }
        }
        [Networked] private int _netOffenseRollResult { get; set; }
        
        public AtBatOutcome CurrentOutcome 
        { 
            get => isLocalMode ? _localCurrentOutcome : _netCurrentOutcome;
            set { if (isLocalMode) _localCurrentOutcome = value; else _netCurrentOutcome = value; }
        }
        [Networked] private AtBatOutcome _netCurrentOutcome { get; set; }
        
        public OptionalActionType AvailableOptionalAction 
        { 
            get => isLocalMode ? _localAvailableOptionalAction : _netAvailableOptionalAction;
            set { if (isLocalMode) _localAvailableOptionalAction = value; else _netAvailableOptionalAction = value; }
        }
        [Networked] private OptionalActionType _netAvailableOptionalAction { get; set; }

        // CPU Mode
        public bool IsCPUGame 
        { 
            get => isLocalMode ? _localIsCPUGame : _netIsCPUGame;
            set { if (isLocalMode) _localIsCPUGame = value; else _netIsCPUGame = value; }
        }
        [Networked] private NetworkBool _netIsCPUGame { get; set; }
        
        public bool CPUIsHome 
        { 
            get => isLocalMode ? _localCPUIsHome : _netCPUIsHome;
            set { if (isLocalMode) _localCPUIsHome = value; else _netCPUIsHome = value; }
        }
        [Networked] private NetworkBool _netCPUIsHome { get; set; }

        // Team lineups (stored as indices into card database)
        [Networked, Capacity(9)] public NetworkArray<int> HomeBatterIndices => default;
        [Networked, Capacity(9)] public NetworkArray<int> AwayBatterIndices => default;
        
        public int HomePitcherIndex 
        { 
            get => isLocalMode ? _localHomePitcherIndex : _netHomePitcherIndex;
            set { if (isLocalMode) _localHomePitcherIndex = value; else _netHomePitcherIndex = value; }
        }
        [Networked] private int _netHomePitcherIndex { get; set; }
        
        public int AwayPitcherIndex 
        { 
            get => isLocalMode ? _localAwayPitcherIndex : _netAwayPitcherIndex;
            set { if (isLocalMode) _localAwayPitcherIndex = value; else _netAwayPitcherIndex = value; }
        }
        [Networked] private int _netAwayPitcherIndex { get; set; }
        
        public int HomeBatterUp 
        { 
            get => isLocalMode ? _localHomeBatterUp : _netHomeBatterUp;
            set { if (isLocalMode) _localHomeBatterUp = value; else _netHomeBatterUp = value; }
        }
        [Networked] private int _netHomeBatterUp { get; set; }
        
        public int AwayBatterUp 
        { 
            get => isLocalMode ? _localAwayBatterUp : _netAwayBatterUp;
            set { if (isLocalMode) _localAwayBatterUp = value; else _netAwayBatterUp = value; }
        }
        [Networked] private int _netAwayBatterUp { get; set; }

        // Defense stats
        public int HomeInfieldDefense 
        { 
            get => isLocalMode ? _localHomeInfieldDefense : _netHomeInfieldDefense;
            set { if (isLocalMode) _localHomeInfieldDefense = value; else _netHomeInfieldDefense = value; }
        }
        [Networked] private int _netHomeInfieldDefense { get; set; }
        
        public int HomeOutfieldDefense 
        { 
            get => isLocalMode ? _localHomeOutfieldDefense : _netHomeOutfieldDefense;
            set { if (isLocalMode) _localHomeOutfieldDefense = value; else _netHomeOutfieldDefense = value; }
        }
        [Networked] private int _netHomeOutfieldDefense { get; set; }
        
        public int HomeCatcherDefense 
        { 
            get => isLocalMode ? _localHomeCatcherDefense : _netHomeCatcherDefense;
            set { if (isLocalMode) _localHomeCatcherDefense = value; else _netHomeCatcherDefense = value; }
        }
        [Networked] private int _netHomeCatcherDefense { get; set; }
        
        public int AwayInfieldDefense 
        { 
            get => isLocalMode ? _localAwayInfieldDefense : _netAwayInfieldDefense;
            set { if (isLocalMode) _localAwayInfieldDefense = value; else _netAwayInfieldDefense = value; }
        }
        [Networked] private int _netAwayInfieldDefense { get; set; }
        
        public int AwayOutfieldDefense 
        { 
            get => isLocalMode ? _localAwayOutfieldDefense : _netAwayOutfieldDefense;
            set { if (isLocalMode) _localAwayOutfieldDefense = value; else _netAwayOutfieldDefense = value; }
        }
        [Networked] private int _netAwayOutfieldDefense { get; set; }
        
        public int AwayCatcherDefense 
        { 
            get => isLocalMode ? _localAwayCatcherDefense : _netAwayCatcherDefense;
            set { if (isLocalMode) _localAwayCatcherDefense = value; else _netAwayCatcherDefense = value; }
        }
        [Networked] private int _netAwayCatcherDefense { get; set; }

        // Local card data cache
        private List<BatterCardData> allBatters;
        private List<PitcherCardData> allPitchers;

        // Events
        public event Action<GameState> OnGameStateChanged;
        public event Action<int, int> OnScoreChanged;
        public event Action<int> OnOutsChanged;
        public event Action<int, bool> OnInningChanged;
        public event Action<AtBatOutcome> OnAtBatComplete;
        public event Action<string> OnGameMessage;
        public event Action OnGameStarted;
        public event Action<int> OnAtBatStarted;
        public event Action<AtBatOutcome> OnAtBatEnded;

        private void Awake()
        {
            // For networked objects, don't set Instance here - let Spawned() handle it
            // Only set Instance if this is a local-only game manager (no NetworkObject)
            var netObj = GetComponent<NetworkObject>();
            if (netObj == null)
            {
                // This is a local-only instance (no networking)
                Instance = this;
                isLocalMode = true;
                InitializeGame();
                Debug.Log("[NetworkGameManager] Awake - Local instance (no NetworkObject)");
            }
            else
            {
                // This has a NetworkObject - wait for Spawned() to set Instance
                Debug.Log("[NetworkGameManager] Awake - Has NetworkObject, waiting for Spawned()");
                // Still initialize card data
                if (allBatters == null)
                {
                    allBatters = CardDatabase.GetSampleBatters();
                    allPitchers = CardDatabase.GetSamplePitchers();
                }
            }
        }

        private void InitializeGame()
        {
            // Initialize card database
            allBatters = CardDatabase.GetSampleBatters();
            allPitchers = CardDatabase.GetSamplePitchers();

            // Find dice roller if not assigned
            if (diceRoller == null)
            {
                diceRoller = FindObjectOfType<DiceRoller3D>();
            }

            // Find base runner controller if not assigned
            if (baseRunnerController == null)
            {
                baseRunnerController = FindObjectOfType<BaseRunnerController>();
            }

            // Subscribe to dice events
            if (diceRoller != null)
            {
                diceRoller.OnDiceRollComplete += HandleDiceRollComplete;
            }

            // Subscribe to base runner events
            if (baseRunnerController != null)
            {
                baseRunnerController.OnRunScored += HandleRunScored;
            }

            // Initialize game state for single player
            CurrentState = GameState.WaitingForPlayers;
            CurrentInning = 1;
            IsTopOfInning = true;
            Outs = 0;
            HomeScore = 0;
            AwayScore = 0;
        }

        public override void Spawned()
        {
            Debug.Log($"[NetworkGameManager] Spawned() called - HasStateAuthority={Object.HasStateAuthority}, InstanceID={GetInstanceID()}");
            Debug.Log($"[NetworkGameManager] Spawned() - HomePlayer={HomePlayer}, AwayPlayer={AwayPlayer}");
            
            // Always set Instance to the spawned networked object
            Instance = this;
            isLocalMode = false; // Use networked properties when properly spawned
            
            Debug.Log($"[NetworkGameManager] Instance set to this networked object. isLocalMode={isLocalMode}");
            
            // Initialize if not already done in Awake
            if (allBatters == null)
            {
                allBatters = CardDatabase.GetSampleBatters();
                allPitchers = CardDatabase.GetSamplePitchers();
            }

            // Find references if not assigned
            if (diceRoller == null)
                diceRoller = FindObjectOfType<DiceRoller3D>();
            if (baseRunnerController == null)
                baseRunnerController = FindObjectOfType<BaseRunnerController>();

            // Subscribe to events on the host (StateAuthority)
            if (Object.HasStateAuthority)
            {
                if (diceRoller != null)
                {
                    diceRoller.OnDiceRollComplete += HandleDiceRollComplete;
                    Debug.Log("[NetworkGameManager] Subscribed to dice roller events on host");
                }
                if (baseRunnerController != null)
                    baseRunnerController.OnRunScored += HandleRunScored;
            }

            if (Object.HasStateAuthority)
            {
                _netCurrentState = GameState.WaitingForPlayers;
                _netCurrentInning = 1;
                _netIsTopOfInning = true;
                _netOuts = 0;
                _netHomeScore = 0;
                _netAwayScore = 0;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (diceRoller != null)
            {
                diceRoller.OnDiceRollComplete -= HandleDiceRollComplete;
            }
            if (baseRunnerController != null)
            {
                baseRunnerController.OnRunScored -= HandleRunScored;
            }
        }

        #region Game Flow Control

        // Local method for single player mode
        public void StartGameLocal(bool vsCPU, bool cpuPlaysHome)
        {
            // Force local mode for CPU games to ensure we use local backing fields
            isLocalMode = true;
            
            // Set BOTH local and networked values to be safe
            _localIsCPUGame = vsCPU;
            _localCPUIsHome = cpuPlaysHome;
            
            Debug.Log($"[NetworkGameManager] StartGameLocal on instance {GetInstanceID()}");
            Debug.Log($"[NetworkGameManager] StartGameLocal - _localIsCPUGame={_localIsCPUGame}, _localCPUIsHome={_localCPUIsHome}, isLocalMode={isLocalMode}");
            Debug.Log($"[NetworkGameManager] StartGameLocal - IsCPUGame={IsCPUGame}, CPUIsHome={CPUIsHome}");
            
            TransitionToState(GameState.RollForTeamAssignment);
        }

        /// <summary>
        /// Start the game. Use this method instead of calling RPC directly - it handles both local and networked modes.
        /// </summary>
        public void StartGame(bool vsCPU, bool cpuPlaysHome)
        {
            // Find the networked instance if this isn't it
            NetworkGameManager targetGM = this;
            if (isLocalMode || Object == null || !Object.IsValid)
            {
                // Try to find the spawned networked instance
                var allGMs = FindObjectsOfType<NetworkGameManager>();
                foreach (var gm in allGMs)
                {
                    if (gm.Object != null && gm.Object.IsValid)
                    {
                        targetGM = gm;
                        Debug.Log("[NetworkGameManager] Found networked instance to use");
                        break;
                    }
                }
            }
            
            Debug.Log($"[NetworkGameManager] StartGame - targetGM.isLocalMode={targetGM.isLocalMode}, targetGM.Object?.IsValid={targetGM.Object?.IsValid}");
            
            if (targetGM.isLocalMode || targetGM.Object == null || !targetGM.Object.IsValid)
            {
                // Local mode or not properly spawned - call directly
                Debug.Log("[NetworkGameManager] Using StartGameLocal (local mode)");
                targetGM.StartGameLocal(vsCPU, cpuPlaysHome);
            }
            else
            {
                // Networked mode - use RPC
                Debug.Log("[NetworkGameManager] Using RPC_StartGame (networked mode)");
                targetGM.RPC_StartGame(vsCPU, cpuPlaysHome);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_StartGame(NetworkBool vsCPU, NetworkBool cpuPlaysHome)
        {
            IsCPUGame = vsCPU;
            CPUIsHome = cpuPlaysHome;
            TransitionToState(GameState.RollForTeamAssignment);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_PlayerReady(PlayerRef player)
        {
            Debug.Log($"[NetworkGameManager] RPC_PlayerReady called for player {player}. Current HomePlayer={HomePlayer}, AwayPlayer={AwayPlayer}");
            
            if (HomePlayer == default)
            {
                HomePlayer = player;
                Debug.Log($"[NetworkGameManager] Assigned {player} as HomePlayer. HomePlayer is now {HomePlayer}");
            }
            else if (AwayPlayer == default && player != HomePlayer)
            {
                AwayPlayer = player;
                Debug.Log($"[NetworkGameManager] Assigned {player} as AwayPlayer. AwayPlayer is now {AwayPlayer}");
            }
            
            // Notify all clients about player assignments
            RPC_NotifyPlayerAssignments(HomePlayer, AwayPlayer);

            // Don't auto-start - wait for host to click "Start Game"
            // The game will start when RPC_StartGame is called
            Debug.Log($"[NetworkGameManager] Players assigned - HomePlayer={HomePlayer}, AwayPlayer={AwayPlayer}");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyPlayerAssignments(PlayerRef home, PlayerRef away)
        {
            Debug.Log($"[NetworkGameManager] RPC_NotifyPlayerAssignments received - HomePlayer={home}, AwayPlayer={away}");
            // The networked properties should already be synced, but this confirms the assignment
        }

        private void TransitionToState(GameState newState)
        {
            // In local mode, skip authority check
            if (!isLocalMode && !Object.HasStateAuthority) return;

            CurrentState = newState;
            
            // Only call RPC in networked mode
            if (!isLocalMode)
            {
                RPC_NotifyStateChange(newState);
            }
            else
            {
                // Directly invoke event in local mode
                OnGameStateChanged?.Invoke(newState);
            }

            switch (newState)
            {
                case GameState.RollForTeamAssignment:
                    // In CPU mode, auto-assign teams and skip to lineups
                    if (IsCPUGame)
                    {
                        // Player is always away (batting first), CPU is home
                        TransitionToState(GameState.SetLineups);
                    }
                    else
                    {
                        // For online multiplayer, also skip to lineups for now
                        // TODO: Implement proper team assignment roll for multiplayer
                        TransitionToState(GameState.SetLineups);
                    }
                    break;

                case GameState.SetLineups:
                    GenerateLineups();
                    TransitionToState(GameState.StartGame);
                    break;

                case GameState.StartGame:
                    SetupFirstAtBat();
                    // Notify all clients that game has started
                    Debug.Log($"[NetworkGameManager] StartGame state - isLocalMode={isLocalMode}, HasStateAuthority={Object?.HasStateAuthority}");
                    if (!isLocalMode)
                    {
                        Debug.Log("[NetworkGameManager] Calling RPC_NotifyGameStarted");
                        RPC_NotifyGameStarted();
                        RPC_NotifyAtBatStarted(CurrentBatterIndex);
                    }
                    else
                    {
                        Debug.Log("[NetworkGameManager] Local mode - invoking OnGameStarted directly");
                        OnGameStarted?.Invoke();
                        OnAtBatStarted?.Invoke(CurrentBatterIndex);
                    }
                    TransitionToState(GameState.DefenseTurn);
                    break;

                case GameState.DefenseTurn:
                    // Wait for defense roll - use local backing fields directly
                    Debug.Log($"[NetworkGameManager] DefenseTurn - _localIsCPUGame={_localIsCPUGame}, ShouldCPURoll={ShouldCPURoll()}, _localIsTopOfInning={_localIsTopOfInning}, _localCPUIsHome={_localCPUIsHome}");
                    if (_localIsCPUGame && ShouldCPURoll())
                    {
                        Debug.Log("[NetworkGameManager] CPU is pitching - CPU will roll for defense");
                        PerformCPURoll();
                    }
                    else if (_localIsCPUGame)
                    {
                        Debug.Log("[NetworkGameManager] Player is pitching - waiting for player to roll for defense");
                    }
                    break;

                case GameState.OffenseTurn:
                    // Wait for offense roll - use local backing field directly
                    Debug.Log($"[NetworkGameManager] OffenseTurn - _localIsCPUGame={_localIsCPUGame}, ShouldCPURoll={ShouldCPURoll()}, _localIsTopOfInning={_localIsTopOfInning}, _localCPUIsHome={_localCPUIsHome}");
                    if (_localIsCPUGame && ShouldCPURoll())
                    {
                        Debug.Log("[NetworkGameManager] CPU is batting - CPU will roll for offense");
                        PerformCPURoll();
                    }
                    else if (_localIsCPUGame)
                    {
                        Debug.Log("[NetworkGameManager] Player is batting - waiting for player to roll for offense");
                    }
                    break;

                case GameState.AtBatAction:
                    ResolveAtBat();
                    break;

                case GameState.OptionalAction:
                    // Wait for player decision
                    if (IsCPUGame && ShouldCPURoll())
                    {
                        DecideCPUOptionalAction();
                    }
                    break;

                case GameState.UpdateBaseRunners:
                    ProcessBaseRunners();
                    break;

                case GameState.NextBatterUp:
                    AdvanceToNextBatter();
                    if (!isLocalMode)
                        RPC_NotifyAtBatStarted(CurrentBatterIndex);
                    else
                        OnAtBatStarted?.Invoke(CurrentBatterIndex);
                    break;

                case GameState.NewHalfInning:
                    StartNewHalfInning();
                    if (!isLocalMode)
                        RPC_NotifyAtBatStarted(CurrentBatterIndex);
                    else
                        OnAtBatStarted?.Invoke(CurrentBatterIndex);
                    break;

                case GameState.EndHalfInning:
                    EndCurrentHalfInning();
                    break;

                case GameState.GameOver:
                    AnnounceGameOver();
                    break;
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyStateChange(GameState newState)
        {
            Debug.Log($"[NetworkGameManager] RPC_NotifyStateChange received - newState={newState}, subscribers={OnGameStateChanged?.GetInvocationList()?.Length ?? 0}");
            OnGameStateChanged?.Invoke(newState);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyGameStarted()
        {
            Debug.Log($"[NetworkGameManager] RPC_NotifyGameStarted received, invoking OnGameStarted (subscribers: {OnGameStarted?.GetInvocationList()?.Length ?? 0})");
            OnGameStarted?.Invoke();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyAtBatStarted(int batterIndex)
        {
            Debug.Log($"[NetworkGameManager] RPC_NotifyAtBatStarted received - batterIndex={batterIndex}");
            OnAtBatStarted?.Invoke(batterIndex);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyAtBatEnded(int outcomeInt)
        {
            var outcome = (AtBatOutcome)outcomeInt;
            Debug.Log($"[NetworkGameManager] RPC_NotifyAtBatEnded received - outcome={outcome}");
            OnAtBatEnded?.Invoke(outcome);
        }

        #endregion

        #region Team Assignment

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SubmitTeamRoll(PlayerRef player, int roll)
        {
            if (CurrentState != GameState.RollForTeamAssignment) return;

            if (player == HomePlayer || (IsCPUGame && !CPUIsHome))
            {
                HomeTeamRoll = roll;
            }
            else
            {
                AwayTeamRoll = roll;
            }

            // Check if both rolls are in
            if (HomeTeamRoll > 0 && AwayTeamRoll > 0)
            {
                // Higher roll gets to choose (we default to home)
                if (AwayTeamRoll > HomeTeamRoll)
                {
                    // Swap players
                    var temp = HomePlayer;
                    HomePlayer = AwayPlayer;
                    AwayPlayer = temp;
                }

                AnnounceTeamAssignment();
                TransitionToState(GameState.SetLineups);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AnnounceTeamAssignment()
        {
            string message = $"Teams assigned! Home roll: {HomeTeamRoll}, Away roll: {AwayTeamRoll}";
            OnGameMessage?.Invoke(message);
        }

        #endregion

        #region Lineup Generation

        private void GenerateLineups()
        {
            System.Random rng = new System.Random();
            
            // Shuffle and pick 9 batters for each team
            List<int> batterPool = new List<int>();
            for (int i = 0; i < allBatters.Count; i++) batterPool.Add(i);
            
            // Fisher-Yates shuffle
            for (int i = batterPool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (batterPool[i], batterPool[j]) = (batterPool[j], batterPool[i]);
            }

            // Assign batters - use local arrays in local mode
            for (int i = 0; i < 9; i++)
            {
                int homeIdx = batterPool[i];
                int awayIdx = batterPool[i + 9 < batterPool.Count ? i + 9 : i];
                
                if (isLocalMode)
                {
                    _localHomeBatterIndices[i] = homeIdx;
                    _localAwayBatterIndices[i] = awayIdx;
                }
                else
                {
                    HomeBatterIndices.Set(i, homeIdx);
                    AwayBatterIndices.Set(i, awayIdx);
                }
            }

            // Assign pitchers
            HomePitcherIndex = rng.Next(allPitchers.Count);
            AwayPitcherIndex = rng.Next(allPitchers.Count);
            if (AwayPitcherIndex == HomePitcherIndex)
            {
                AwayPitcherIndex = (AwayPitcherIndex + 1) % allPitchers.Count;
            }

            // Calculate defense stats
            CalculateDefenseStats();

            // Reset lineup positions
            HomeBatterUp = 0;
            AwayBatterUp = 0;
        }

        private void CalculateDefenseStats()
        {
            // Home team defense
            int homeInfield = 0, homeOutfield = 0, homeCatcher = 0;
            for (int i = 0; i < 9; i++)
            {
                int idx = isLocalMode ? _localHomeBatterIndices[i] : HomeBatterIndices.Get(i);
                var batter = allBatters[idx];
                string pos = batter.Position;
                
                if (pos == "C") homeCatcher += batter.PositionPlus;
                else if (pos == "1B" || pos == "2B" || pos == "SS" || pos == "3B")
                    homeInfield += batter.PositionPlus;
                else
                    homeOutfield += batter.PositionPlus;
            }
            HomeInfieldDefense = homeInfield;
            HomeOutfieldDefense = homeOutfield;
            HomeCatcherDefense = homeCatcher;

            // Away team defense
            int awayInfield = 0, awayOutfield = 0, awayCatcher = 0;
            for (int i = 0; i < 9; i++)
            {
                int idx = isLocalMode ? _localAwayBatterIndices[i] : AwayBatterIndices.Get(i);
                var batter = allBatters[idx];
                string pos = batter.Position;
                
                if (pos == "C") awayCatcher += batter.PositionPlus;
                else if (pos == "1B" || pos == "2B" || pos == "SS" || pos == "3B")
                    awayInfield += batter.PositionPlus;
                else
                    awayOutfield += batter.PositionPlus;
            }
            AwayInfieldDefense = awayInfield;
            AwayOutfieldDefense = awayOutfield;
            AwayCatcherDefense = awayCatcher;
        }

        #endregion

        #region At-Bat Logic

        private void SetupFirstAtBat()
        {
            // Top of first - away team bats
            // Set both local and property to be safe
            _localIsTopOfInning = true;
            IsTopOfInning = true;
            CurrentBatterIndex = GetBatterIndex(true, AwayBatterUp);
            if (baseRunnerController != null)
                baseRunnerController.ClearBases();
            else
                ClearSimulatedBases();
            
            Debug.Log($"[NetworkGameManager] SetupFirstAtBat - _localIsTopOfInning={_localIsTopOfInning}, isLocalMode={isLocalMode}");
        }

        // Helper to get batter index from local or networked array
        private int GetBatterIndex(bool isAway, int position)
        {
            if (isLocalMode)
            {
                return isAway ? _localAwayBatterIndices[position] : _localHomeBatterIndices[position];
            }
            else
            {
                return isAway ? AwayBatterIndices.Get(position) : HomeBatterIndices.Get(position);
            }
        }

        // Local method for single player mode
        public void RollDiceLocal()
        {
            if (CurrentState == GameState.DefenseTurn ||
                CurrentState == GameState.OffenseTurn ||
                CurrentState == GameState.OptionalAction ||
                CurrentState == GameState.RollForTeamAssignment)
            {
                // In local mode, simulate dice roll directly
                int result = UnityEngine.Random.Range(1, 21);
                HandleDiceRollComplete(result);
            }
        }

        // Public method for UI to request dice roll (handles both local and online)
        public void RequestDiceRoll()
        {
            Debug.Log($"[NetworkGameManager] RequestDiceRoll called - isLocalMode={isLocalMode}, IsCPUGame={IsCPUGame}, Runner={Runner != null}");
            
            // In CPU game, only allow roll if it's the player's turn (not CPU's turn)
            if (IsCPUGame && ShouldCPURoll())
            {
                Debug.Log("[NetworkGameManager] RequestDiceRoll ignored - it's CPU's turn");
                return;
            }
            
            if (isLocalMode || IsCPUGame)
            {
                // Use the dice roller with visuals
                RequestDiceRollInternal();
            }
            else
            {
                // Online mode - call RPC
                var runner = NetworkRunnerHandler.Instance?.Runner;
                if (runner != null && runner.LocalPlayer != PlayerRef.None)
                {
                    Debug.Log($"[NetworkGameManager] Calling RPC_RollDice for player {runner.LocalPlayer}");
                    RPC_RollDice(runner.LocalPlayer);
                }
                else
                {
                    Debug.LogWarning($"[NetworkGameManager] Cannot call RPC_RollDice - runner={runner != null}, LocalPlayer={runner?.LocalPlayer}");
                }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RollDice(PlayerRef player)
        {
            Debug.Log($"[RPC_RollDice] Received from player {player}, CurrentState={CurrentState}, IsTopOfInning={IsTopOfInning}");
            
            // Determine if this player is the host (Home team) or client (Away team)
            // Host has the lower PlayerRef ID typically, or we can check if they match the server's local player
            bool isPlayerHost = (player == Runner.LocalPlayer && Runner.IsServer) || 
                               (player != Runner.LocalPlayer && !Runner.IsServer);
            // Actually simpler: the host's LocalPlayer is the home team
            // We need to check if the requesting player should be acting
            
            // For now, trust the client and process the roll - the UI already checks IsLocalPlayerTurn
            if (CurrentState == GameState.DefenseTurn ||
                CurrentState == GameState.OffenseTurn ||
                CurrentState == GameState.OptionalAction)
            {
                Debug.Log($"[RPC_RollDice] Processing roll for player {player}");
                RequestDiceRollInternal();
            }
            else if (CurrentState == GameState.RollForTeamAssignment)
            {
                RequestDiceRollInternal();
            }
        }
        
        /// <summary>
        /// Internal method to request a dice roll, using local or RPC based on mode.
        /// </summary>
        private void RequestDiceRollInternal()
        {
            if (diceRoller == null) return;
            
            // Always use RequestRollLocal since DiceRoller3D may not be a spawned network object
            // The dice roll result is handled locally on the host and state changes are synced via RPC
            diceRoller.RequestRollLocal(20);
        }

        /// <summary>
        /// Public method to process a dice result from external sources (like 3D dice UI)
        /// </summary>
        public void ProcessDiceResult(int result)
        {
            Debug.Log($"[NetworkGameManager] ProcessDiceResult called with result: {result}");
            HandleDiceRollComplete(result);
        }
        
        private void HandleDiceRollComplete(int result)
        {
            // In local mode, skip authority check
            if (!isLocalMode && !Object.HasStateAuthority) return;

            switch (CurrentState)
            {
                case GameState.RollForTeamAssignment:
                    // Handle in RPC_SubmitTeamRoll
                    break;

                case GameState.DefenseTurn:
                    ProcessDefenseRoll(result);
                    break;

                case GameState.OffenseTurn:
                    ProcessOffenseRoll(result);
                    break;

                case GameState.OptionalAction:
                    ProcessOptionalActionRoll(result);
                    break;
            }
        }

        private void ProcessDefenseRoll(int roll)
        {
            DefenseRollResult = roll;

            // Get pitcher control
            int pitcherIndex = IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex;
            var pitcher = allPitchers[pitcherIndex];
            int defenseTotal = roll + pitcher.Control;

            // Get batter OnBase
            var batter = allBatters[CurrentBatterIndex];
            
            // Determine advantage: if defense total > batter OnBase, pitcher wins (use pitcher chart)
            // if defense total <= batter OnBase, batter wins (use batter chart)
            BatterHasAdvantage = defenseTotal <= batter.OnBase;

            string chartUsed = BatterHasAdvantage ? "BATTER'S" : "PITCHER'S";
            string message = $"[DEFENSE] {pitcher.PlayerName} rolls {roll} + {pitcher.Control} Control = {defenseTotal}";
            message += $"\nvs {batter.PlayerName}'s OnBase {batter.OnBase}";
            message += $"\n→ Using {chartUsed} chart!";
            SendGameMessage(message);

            TransitionToState(GameState.OffenseTurn);
        }

        private void ProcessOffenseRoll(int roll)
        {
            OffenseRollResult = roll;

            // Determine outcome based on advantage
            OutcomeCard outcomeCard;
            string chartOwner;
            if (BatterHasAdvantage)
            {
                outcomeCard = allBatters[CurrentBatterIndex].OutcomeCard;
                chartOwner = allBatters[CurrentBatterIndex].PlayerName;
            }
            else
            {
                int pitcherIndex = IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex;
                outcomeCard = allPitchers[pitcherIndex].OutcomeCard;
                chartOwner = allPitchers[pitcherIndex].PlayerName;
            }

            CurrentOutcome = outcomeCard.GetOutcome(roll);

            string message = $"[OFFENSE] Rolls {roll} on {chartOwner}'s chart";
            message += $"\n→ Result: {GetOutcomeText(CurrentOutcome)}!";
            SendGameMessage(message);

            TransitionToState(GameState.AtBatAction);
        }

        private void ResolveAtBat()
        {
            // Update batter stats
            var batter = allBatters[CurrentBatterIndex];
            batter.AtBats++;

            // Check for optional actions
            AvailableOptionalAction = DetermineOptionalAction();

            if (AvailableOptionalAction != OptionalActionType.None)
            {
                TransitionToState(GameState.OptionalAction);
            }
            else
            {
                TransitionToState(GameState.UpdateBaseRunners);
            }
        }

        private OptionalActionType DetermineOptionalAction()
        {
            switch (CurrentOutcome)
            {
                case AtBatOutcome.Strikeout:
                    // Can attempt stolen base if runner on base
                    if (baseRunnerController != null && baseRunnerController.RunnersOnBase > 0)
                        return OptionalActionType.StolenBase;
                    break;

                case AtBatOutcome.Flyout:
                    // Can attempt tag up if runner on third
                    if (baseRunnerController != null && baseRunnerController.HasRunnerOn(Base.Third))
                        return OptionalActionType.TagUp;
                    break;

                case AtBatOutcome.Groundout:
                    // Defense can attempt double play if runner on first
                    if (baseRunnerController != null && baseRunnerController.HasRunnerOn(Base.First) && Outs < 2)
                        return OptionalActionType.DoublePlay;
                    break;
            }
            return OptionalActionType.None;
        }

        // Local methods for single player mode
        public void DeclineOptionalActionLocal()
        {
            if (CurrentState != GameState.OptionalAction) return;
            AvailableOptionalAction = OptionalActionType.None;
            TransitionToState(GameState.UpdateBaseRunners);
        }

        public void AttemptOptionalActionLocal()
        {
            if (CurrentState != GameState.OptionalAction) return;
            // Roll will be handled by RollDiceLocal
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_DeclineOptionalAction()
        {
            if (CurrentState != GameState.OptionalAction) return;
            AvailableOptionalAction = OptionalActionType.None;
            TransitionToState(GameState.UpdateBaseRunners);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_AttemptOptionalAction()
        {
            if (CurrentState != GameState.OptionalAction) return;
            // Roll will be handled by RPC_RollDice
        }

        private void ProcessOptionalActionRoll(int roll)
        {
            bool success = false;
            int defense;

            switch (AvailableOptionalAction)
            {
                case OptionalActionType.StolenBase:
                    defense = IsTopOfInning ? HomeCatcherDefense : AwayCatcherDefense;
                    if (baseRunnerController != null)
                    {
                        // Try to steal from first base first, then second
                        if (baseRunnerController.HasRunnerOn(Base.Second))
                            success = baseRunnerController.AttemptSteal(Base.Second, defense, roll);
                        else if (baseRunnerController.HasRunnerOn(Base.First))
                            success = baseRunnerController.AttemptSteal(Base.First, defense, roll);
                    }
                    else
                    {
                        // Simple simulation: 50% chance based on roll vs defense
                        success = roll > defense + 5;
                    }
                    if (!success) Outs++;
                    SendGameMessage(success ? "Stolen base successful!" : "Caught stealing!");
                    break;

                case OptionalActionType.TagUp:
                    defense = IsTopOfInning ? HomeOutfieldDefense : AwayOutfieldDefense;
                    if (baseRunnerController != null)
                    {
                        success = baseRunnerController.AttemptTagUp(Base.Third, defense, roll);
                    }
                    else
                    {
                        success = roll > defense + 5;
                    }
                    SendGameMessage(success ? "Tag up successful! Run scores!" : "Runner held at third!");
                    break;

                case OptionalActionType.DoublePlay:
                    defense = IsTopOfInning ? HomeInfieldDefense : AwayInfieldDefense;
                    if (baseRunnerController != null)
                    {
                        success = baseRunnerController.AttemptDoublePlay(defense, roll);
                    }
                    else
                    {
                        success = roll <= defense + 5;
                    }
                    if (success) Outs++;
                    SendGameMessage(success ? "Double play!" : "Runner safe at second!");
                    break;
            }

            AvailableOptionalAction = OptionalActionType.None;
            TransitionToState(GameState.UpdateBaseRunners);
        }

        #endregion

        #region Base Runner Processing

        private void ProcessBaseRunners()
        {
            int runsScored = 0;
            var batter = allBatters[CurrentBatterIndex];

            switch (CurrentOutcome)
            {
                case AtBatOutcome.Strikeout:
                case AtBatOutcome.Groundout:
                case AtBatOutcome.Flyout:
                    Outs++;
                    NotifyOutsChanged();
                    
                    // Update pitcher stats
                    int pitcherIdx = IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex;
                    if (CurrentOutcome == AtBatOutcome.Strikeout)
                        allPitchers[pitcherIdx].Strikeouts++;
                    break;

                case AtBatOutcome.Walk:
                    runsScored = baseRunnerController != null ? 
                        baseRunnerController.ProcessWalk(CurrentBatterIndex, batter.Speed) : 
                        SimulateWalk();
                    allPitchers[IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex].Walks++;
                    break;

                case AtBatOutcome.Single:
                    runsScored = baseRunnerController != null ? 
                        baseRunnerController.ProcessSingle(CurrentBatterIndex, batter.Speed) : 
                        SimulateSingle();
                    batter.Hits++;
                    allPitchers[IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex].Hits++;
                    break;

                case AtBatOutcome.Double:
                    runsScored = baseRunnerController != null ? 
                        baseRunnerController.ProcessDouble(CurrentBatterIndex, batter.Speed) : 
                        SimulateDouble();
                    batter.Hits++;
                    allPitchers[IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex].Hits++;
                    break;

                case AtBatOutcome.Triple:
                    runsScored = baseRunnerController != null ? 
                        baseRunnerController.ProcessTriple(CurrentBatterIndex, batter.Speed) : 
                        SimulateTriple();
                    batter.Hits++;
                    allPitchers[IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex].Hits++;
                    break;

                case AtBatOutcome.HomeRun:
                    runsScored = baseRunnerController != null ? 
                        baseRunnerController.ProcessHomeRun(CurrentBatterIndex) : 
                        SimulateHomeRun();
                    batter.Hits++;
                    batter.HomeRuns++;
                    allPitchers[IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex].Hits++;
                    break;
            }

            // Update RBIs and runs
            if (runsScored > 0)
            {
                batter.RBIs += runsScored;
                
                if (IsTopOfInning)
                    AwayScore += runsScored;
                else
                    HomeScore += runsScored;

                NotifyScoreChanged();
                
                // Update pitcher earned runs
                allPitchers[IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex].EarnedRuns += runsScored;
            }

            OnAtBatComplete?.Invoke(CurrentOutcome);
            
            // Notify all clients about at-bat end
            if (!isLocalMode)
                RPC_NotifyAtBatEnded((int)CurrentOutcome);
            else
                OnAtBatEnded?.Invoke(CurrentOutcome);

            // Check for 3 outs
            if (Outs >= 3)
            {
                TransitionToState(GameState.EndHalfInning);
            }
            else
            {
                TransitionToState(GameState.NextBatterUp);
            }
        }

        private void HandleRunScored(int runs)
        {
            // Already handled in ProcessBaseRunners
        }

        // Simple base runner simulation when baseRunnerController is null
        // Tracks runners on base locally
        private int _runnersOnBase = 0; // Bitmask: 1=first, 2=second, 4=third

        private int SimulateWalk()
        {
            int runs = 0;
            // Push runners forward if bases loaded
            if ((_runnersOnBase & 7) == 7) // Bases loaded
            {
                runs = 1;
            }
            else if ((_runnersOnBase & 3) == 3) // First and second
            {
                _runnersOnBase |= 4; // Runner to third
            }
            else if ((_runnersOnBase & 1) == 1) // Runner on first
            {
                _runnersOnBase |= 2; // Runner to second
            }
            _runnersOnBase |= 1; // Batter to first
            return runs;
        }

        private int SimulateSingle()
        {
            int runs = 0;
            // Runner on third scores
            if ((_runnersOnBase & 4) != 0) { runs++; _runnersOnBase &= ~4; }
            // Runner on second goes to third (or scores with speed)
            if ((_runnersOnBase & 2) != 0) { _runnersOnBase &= ~2; _runnersOnBase |= 4; }
            // Runner on first goes to second
            if ((_runnersOnBase & 1) != 0) { _runnersOnBase &= ~1; _runnersOnBase |= 2; }
            // Batter to first
            _runnersOnBase |= 1;
            return runs;
        }

        private int SimulateDouble()
        {
            int runs = 0;
            // Runner on third scores
            if ((_runnersOnBase & 4) != 0) { runs++; _runnersOnBase &= ~4; }
            // Runner on second scores
            if ((_runnersOnBase & 2) != 0) { runs++; _runnersOnBase &= ~2; }
            // Runner on first goes to third
            if ((_runnersOnBase & 1) != 0) { _runnersOnBase &= ~1; _runnersOnBase |= 4; }
            // Batter to second
            _runnersOnBase |= 2;
            return runs;
        }

        private int SimulateTriple()
        {
            int runs = 0;
            // All runners score
            if ((_runnersOnBase & 4) != 0) { runs++; }
            if ((_runnersOnBase & 2) != 0) { runs++; }
            if ((_runnersOnBase & 1) != 0) { runs++; }
            _runnersOnBase = 4; // Batter to third
            return runs;
        }

        private int SimulateHomeRun()
        {
            int runs = 1; // Batter scores
            // All runners score
            if ((_runnersOnBase & 4) != 0) runs++;
            if ((_runnersOnBase & 2) != 0) runs++;
            if ((_runnersOnBase & 1) != 0) runs++;
            _runnersOnBase = 0; // Clear bases
            return runs;
        }

        private void ClearSimulatedBases()
        {
            _runnersOnBase = 0;
        }

        #endregion

        #region Inning Management

        private void AdvanceToNextBatter()
        {
            if (IsTopOfInning)
            {
                AwayBatterUp = (AwayBatterUp + 1) % 9;
                CurrentBatterIndex = GetBatterIndex(true, AwayBatterUp);
            }
            else
            {
                HomeBatterUp = (HomeBatterUp + 1) % 9;
                CurrentBatterIndex = GetBatterIndex(false, HomeBatterUp);
            }

            TransitionToState(GameState.DefenseTurn);
        }

        private void EndCurrentHalfInning()
        {
            // Update pitcher innings
            int pitcherIdx = IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex;
            allPitchers[pitcherIdx].InningsPitched += 0.5f;

            // Clear bases
            if (baseRunnerController != null)
                baseRunnerController.ClearBases();
            else
                ClearSimulatedBases();
            Outs = 0;

            if (IsTopOfInning)
            {
                // Switch to bottom of inning
                IsTopOfInning = false;
                TransitionToState(GameState.NewHalfInning);
            }
            else
            {
                // Check for game end
                if (CurrentInning >= totalInnings && HomeScore != AwayScore)
                {
                    TransitionToState(GameState.GameOver);
                }
                else if (CurrentInning >= totalInnings && HomeScore > AwayScore)
                {
                    // Walk-off scenario already handled
                    TransitionToState(GameState.GameOver);
                }
                else
                {
                    // Next inning
                    CurrentInning++;
                    IsTopOfInning = true;
                    NotifyInningChanged();
                    TransitionToState(GameState.NewHalfInning);
                }
            }
        }

        private void StartNewHalfInning()
        {
            // Set up batter
            if (IsTopOfInning)
            {
                CurrentBatterIndex = GetBatterIndex(true, AwayBatterUp);
            }
            else
            {
                CurrentBatterIndex = GetBatterIndex(false, HomeBatterUp);
            }

            NotifyInningChanged();
            TransitionToState(GameState.DefenseTurn);
        }

        #endregion

        #region CPU Logic

        private bool cpuRollInProgress = false;
        
        private bool ShouldCPURoll()
        {
            // Use local backing fields directly
            if (!_localIsCPUGame) 
            {
                return false;
            }

            // CPU is HOME team, Player is AWAY team
            // Top of inning: AWAY (player) bats, HOME (CPU) pitches
            // Bottom of inning: HOME (CPU) bats, AWAY (player) pitches
            
            // In TOP of inning:
            //   - DefenseTurn: CPU pitches -> CPU should roll
            //   - OffenseTurn: Player bats -> Player should roll (CPU should NOT roll)
            // In BOTTOM of inning:
            //   - DefenseTurn: Player pitches -> Player should roll (CPU should NOT roll)
            //   - OffenseTurn: CPU bats -> CPU should roll
            
            bool topOfInning = _localIsTopOfInning;
            
            Debug.Log($"[ShouldCPURoll] _localCPUIsHome={_localCPUIsHome}, topOfInning={topOfInning}, CurrentState={CurrentState}");

            if (_localCPUIsHome)
            {
                // CPU is home team
                switch (CurrentState)
                {
                    case GameState.DefenseTurn:
                        // CPU pitches in top of inning (when away team bats)
                        Debug.Log($"[ShouldCPURoll] DefenseTurn - CPU is home, returning topOfInning={topOfInning}");
                        return topOfInning;
                        
                    case GameState.OffenseTurn:
                        // CPU bats in bottom of inning
                        Debug.Log($"[ShouldCPURoll] OffenseTurn - CPU is home, returning !topOfInning={!topOfInning}");
                        return !topOfInning;
                        
                    case GameState.OptionalAction:
                        // CPU decides in bottom of inning (when CPU bats)
                        return !topOfInning;
                    
                    default:
                        return false;
                }
            }
            else
            {
                // CPU is away team (less common setup)
                switch (CurrentState)
                {
                    case GameState.DefenseTurn:
                        // CPU pitches in bottom of inning
                        return !topOfInning;
                        
                    case GameState.OffenseTurn:
                        // CPU bats in top of inning
                        return topOfInning;
                        
                    case GameState.OptionalAction:
                        return topOfInning;
                    
                    default:
                        return false;
                }
            }
        }

        private void PerformCPURoll()
        {
            // Prevent multiple CPU rolls from being triggered
            if (cpuRollInProgress) return;
            cpuRollInProgress = true;
            
            // Delay for visual effect - use this MonoBehaviour in local mode
            if (isLocalMode)
            {
                StartCoroutine(DelayedCPURoll());
            }
            else
            {
                Runner.GetComponent<MonoBehaviour>().StartCoroutine(DelayedCPURoll());
            }
        }

        private System.Collections.IEnumerator DelayedCPURoll()
        {
            yield return new WaitForSeconds(0.5f);
            cpuRollInProgress = false;
            
            // In local/CPU mode, roll directly; in networked mode, use RPC
            if (isLocalMode || IsCPUGame)
            {
                // Use local roll method that doesn't require NetworkBehaviour initialization
                diceRoller?.RequestRollLocal(20);
            }
            else
            {
                diceRoller?.RPC_RequestRoll(20);
            }
        }

        private void DecideCPUOptionalAction()
        {
            // Simple AI: attempt optional action if odds are favorable
            bool shouldAttempt = false;

            switch (AvailableOptionalAction)
            {
                case OptionalActionType.StolenBase:
                    // Attempt if runner has high speed
                    if (baseRunnerController != null)
                    {
                        var runner = baseRunnerController.GetRunnerOn(Base.First) ?? 
                                     baseRunnerController.GetRunnerOn(Base.Second);
                        if (runner.HasValue && runner.Value.Speed >= 14)
                            shouldAttempt = true;
                    }
                    else
                    {
                        // In simulation mode, 50% chance to attempt
                        shouldAttempt = UnityEngine.Random.value > 0.5f;
                    }
                    break;

                case OptionalActionType.TagUp:
                    // Usually attempt sac fly with runner on third
                    shouldAttempt = true;
                    break;

                case OptionalActionType.DoublePlay:
                    // Defense always attempts double play
                    shouldAttempt = true;
                    break;
            }

            if (shouldAttempt)
            {
                // Use this MonoBehaviour in local mode
                if (isLocalMode)
                {
                    StartCoroutine(DelayedCPURoll());
                }
                else
                {
                    Runner.GetComponent<MonoBehaviour>().StartCoroutine(DelayedCPURoll());
                }
            }
            else
            {
                DeclineOptionalActionLocal();
            }
        }

        #endregion

        #region Network RPCs and Local Wrappers

        // Wrapper methods that route to local or RPC based on mode
        private void SendGameMessage(string message)
        {
            if (isLocalMode)
            {
                OnGameMessage?.Invoke(message);
            }
            else
            {
                RPC_SendGameMessage(message);
            }
        }

        private void NotifyScoreChanged()
        {
            if (isLocalMode)
            {
                OnScoreChanged?.Invoke(HomeScore, AwayScore);
            }
            else
            {
                RPC_NotifyScoreChanged(HomeScore, AwayScore);
            }
        }

        private void NotifyOutsChanged()
        {
            if (isLocalMode)
            {
                OnOutsChanged?.Invoke(Outs);
            }
            else
            {
                RPC_NotifyOutsChanged(Outs);
            }
        }

        private void NotifyInningChanged()
        {
            if (isLocalMode)
            {
                OnInningChanged?.Invoke(CurrentInning, IsTopOfInning);
            }
            else
            {
                RPC_NotifyInningChanged(CurrentInning, IsTopOfInning);
            }
        }

        private void AnnounceGameOver()
        {
            string winner = HomeScore > AwayScore ? "HOME" : "AWAY";
            string message = $"GAME OVER! {winner} wins {Mathf.Max(HomeScore, AwayScore)}-{Mathf.Min(HomeScore, AwayScore)}!";
            if (isLocalMode)
            {
                OnGameMessage?.Invoke(message);
            }
            else
            {
                RPC_AnnounceGameOver();
            }
        }

        private void AnnounceTeamAssignment()
        {
            string message = $"Teams assigned! Home roll: {HomeTeamRoll}, Away roll: {AwayTeamRoll}";
            if (isLocalMode)
            {
                OnGameMessage?.Invoke(message);
            }
            else
            {
                RPC_AnnounceTeamAssignment();
            }
        }

        // Actual RPC methods (only called in networked mode)
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SendGameMessage(string message)
        {
            OnGameMessage?.Invoke(message);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyScoreChanged(int home, int away)
        {
            OnScoreChanged?.Invoke(home, away);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyOutsChanged(int outs)
        {
            OnOutsChanged?.Invoke(outs);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyInningChanged(int inning, NetworkBool isTop)
        {
            OnInningChanged?.Invoke(inning, isTop);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AnnounceGameOver()
        {
            string winner = HomeScore > AwayScore ? "HOME" : "AWAY";
            OnGameMessage?.Invoke($"GAME OVER! {winner} wins {Mathf.Max(HomeScore, AwayScore)}-{Mathf.Min(HomeScore, AwayScore)}!");
        }

        #endregion

        #region Public Getters

        public BatterCardData GetCurrentBatter()
        {
            if (allBatters == null || CurrentBatterIndex < 0 || CurrentBatterIndex >= allBatters.Count)
                return null;
            return allBatters[CurrentBatterIndex];
        }

        public PitcherCardData GetCurrentPitcher()
        {
            if (allPitchers == null) return null;
            int idx = IsTopOfInning ? HomePitcherIndex : AwayPitcherIndex;
            if (idx < 0 || idx >= allPitchers.Count) return null;
            return allPitchers[idx];
        }

        public PitcherCardData GetHomePitcher()
        {
            if (allPitchers == null) return null;
            if (HomePitcherIndex < 0 || HomePitcherIndex >= allPitchers.Count) return null;
            return allPitchers[HomePitcherIndex];
        }

        public PitcherCardData GetAwayPitcher()
        {
            if (allPitchers == null) return null;
            if (AwayPitcherIndex < 0 || AwayPitcherIndex >= allPitchers.Count) return null;
            return allPitchers[AwayPitcherIndex];
        }

        public BatterCardData GetBatter(int index)
        {
            if (allBatters == null || index < 0 || index >= allBatters.Count)
                return null;
            return allBatters[index];
        }

        public PitcherCardData GetPitcher(int index)
        {
            if (allPitchers == null || index < 0 || index >= allPitchers.Count)
                return null;
            return allPitchers[index];
        }

        public bool IsLocalPlayerTurn(PlayerRef localPlayer)
        {
            if (CurrentState == GameState.DefenseTurn)
            {
                return IsTopOfInning ? (localPlayer == HomePlayer) : (localPlayer == AwayPlayer);
            }
            else if (CurrentState == GameState.OffenseTurn)
            {
                return IsTopOfInning ? (localPlayer == AwayPlayer) : (localPlayer == HomePlayer);
            }
            return false;
        }

        // Returns base runner state as bitmask: 1=first, 2=second, 4=third
        public int GetRunnersOnBase()
        {
            if (baseRunnerController != null)
            {
                int runners = 0;
                if (baseRunnerController.HasRunnerOn(Base.First)) runners |= 1;
                if (baseRunnerController.HasRunnerOn(Base.Second)) runners |= 2;
                if (baseRunnerController.HasRunnerOn(Base.Third)) runners |= 4;
                return runners;
            }
            return _runnersOnBase;
        }

        public bool HasRunnerOnFirst() => (GetRunnersOnBase() & 1) != 0;
        public bool HasRunnerOnSecond() => (GetRunnersOnBase() & 2) != 0;
        public bool HasRunnerOnThird() => (GetRunnersOnBase() & 4) != 0;

        // Get lineup arrays for UI display
        public BatterCardData[] GetAwayBatters()
        {
            if (allBatters == null) return null;
            var indices = GetAwayBatterIndices();
            if (indices == null || indices.Length == 0) return null;
            
            BatterCardData[] batters = new BatterCardData[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] >= 0 && indices[i] < allBatters.Count)
                    batters[i] = allBatters[indices[i]];
            }
            return batters;
        }

        public BatterCardData[] GetHomeBatters()
        {
            if (allBatters == null) return null;
            var indices = GetHomeBatterIndices();
            if (indices == null || indices.Length == 0) return null;
            
            BatterCardData[] batters = new BatterCardData[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] >= 0 && indices[i] < allBatters.Count)
                    batters[i] = allBatters[indices[i]];
            }
            return batters;
        }

        public int GetCurrentBatterLineupIndex()
        {
            // Returns the lineup position (0-8) of the current batter
            if (IsTopOfInning)
                return AwayBatterUp;
            else
                return HomeBatterUp;
        }

        // Get runners on specific bases (for displaying their card info)
        public BatterCardData GetRunnerOnFirst()
        {
            if (!HasRunnerOnFirst()) return null;
            // In a full implementation, we'd track which batter is on which base
            // For now, return null - the UI will just show a generic runner
            return null;
        }

        public BatterCardData GetRunnerOnSecond()
        {
            if (!HasRunnerOnSecond()) return null;
            return null;
        }

        public BatterCardData GetRunnerOnThird()
        {
            if (!HasRunnerOnThird()) return null;
            return null;
        }

        private int[] GetHomeBatterIndices()
        {
            if (isLocalMode)
                return _localHomeBatterIndices;
            
            if (!Object.IsValid) return _localHomeBatterIndices;
            
            int[] indices = new int[9];
            for (int i = 0; i < 9; i++)
                indices[i] = HomeBatterIndices.Get(i);
            return indices;
        }

        private int[] GetAwayBatterIndices()
        {
            if (isLocalMode)
                return _localAwayBatterIndices;
            
            if (!Object.IsValid) return _localAwayBatterIndices;
            
            int[] indices = new int[9];
            for (int i = 0; i < 9; i++)
                indices[i] = AwayBatterIndices.Get(i);
            return indices;
        }

        private string GetOutcomeText(AtBatOutcome outcome)
        {
            return outcome switch
            {
                AtBatOutcome.Strikeout => "STRIKEOUT",
                AtBatOutcome.Groundout => "GROUND OUT",
                AtBatOutcome.Flyout => "FLY OUT",
                AtBatOutcome.Walk => "WALK",
                AtBatOutcome.Single => "SINGLE",
                AtBatOutcome.Double => "DOUBLE",
                AtBatOutcome.Triple => "TRIPLE",
                AtBatOutcome.HomeRun => "HOME RUN",
                _ => "UNKNOWN"
            };
        }

        #endregion
    }
}
