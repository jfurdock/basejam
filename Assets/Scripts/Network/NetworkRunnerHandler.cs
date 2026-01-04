using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;

namespace MLBShowdown.Network
{
    public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static NetworkRunnerHandler Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private NetworkPrefabRef gameManagerPrefab;
        [SerializeField] private NetworkPrefabRef playerPrefab;
        
        // Runtime-loaded prefab (used when SerializeField isn't assigned)
        private NetworkObject loadedGameManagerPrefab;

        [Header("Settings")]
        [SerializeField] private string defaultRoomName = "MLBShowdown";

        public NetworkRunner Runner { get; private set; }
        public PlayerRef LocalPlayer { get; private set; }

        public event Action OnConnectedToServerEvent;
        public event Action OnDisconnectedFromServerEvent;
        public event Action<PlayerRef> OnPlayerJoinedEvent;
        public event Action<PlayerRef> OnPlayerLeftEvent;

        private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Auto-load prefabs from Resources if not assigned
            LoadPrefabsFromResources();
        }
        
        private void LoadPrefabsFromResources()
        {
            // Load NetworkGameManager prefab from Resources if not assigned via Inspector
            if (!gameManagerPrefab.IsValid)
            {
                loadedGameManagerPrefab = Resources.Load<NetworkObject>("NetworkGameManager");
                if (loadedGameManagerPrefab != null)
                {
                    Debug.Log("[NetworkRunnerHandler] Loaded NetworkGameManager prefab from Resources");
                }
                else
                {
                    Debug.LogWarning("[NetworkRunnerHandler] NetworkGameManager prefab not found in Resources folder. Multiplayer may not work correctly.");
                }
            }
        }
        
        private bool HasGameManagerPrefab()
        {
            return gameManagerPrefab.IsValid || loadedGameManagerPrefab != null;
        }
        
        private NetworkObject SpawnGameManager(NetworkRunner runner)
        {
            if (gameManagerPrefab.IsValid)
            {
                return runner.Spawn(gameManagerPrefab, Vector3.zero, Quaternion.identity);
            }
            else if (loadedGameManagerPrefab != null)
            {
                return runner.Spawn(loadedGameManagerPrefab, Vector3.zero, Quaternion.identity);
            }
            return null;
        }

        public async Task<bool> StartGame(GameMode mode, string roomName = null)
        {
            if (Runner != null)
            {
                await Runner.Shutdown();
            }

            Runner = gameObject.AddComponent<NetworkRunner>();
            Runner.ProvideInput = true;

            var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

            var result = await Runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = roomName ?? defaultRoomName,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = sceneManager
            });

            if (result.Ok)
            {
                Debug.Log($"Started game in {mode} mode, room: {roomName ?? defaultRoomName}");
                return true;
            }
            else
            {
                Debug.LogError($"Failed to start game: {result.ShutdownReason}");
                return false;
            }
        }

        public async Task<bool> JoinGame(string roomName, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                Debug.Log($"[NetworkRunnerHandler] Attempting to join room '{roomName}' (attempt {i + 1}/{maxRetries})");
                bool success = await StartGame(GameMode.Client, roomName);
                if (success)
                {
                    return true;
                }
                
                // Wait before retrying
                if (i < maxRetries - 1)
                {
                    Debug.Log($"[NetworkRunnerHandler] Room not found, retrying in 2 seconds...");
                    await Task.Delay(2000);
                }
            }
            
            Debug.LogError($"[NetworkRunnerHandler] Failed to join room '{roomName}' after {maxRetries} attempts");
            return false;
        }

        public async Task<bool> HostGame(string roomName = null)
        {
            return await StartGame(GameMode.Host, roomName);
        }

        public async Task<bool> StartSinglePlayer()
        {
            return await StartGame(GameMode.Single);
        }

        public async void Disconnect()
        {
            if (Runner != null)
            {
                await Runner.Shutdown();
                Runner = null;
            }
        }

        private void CreateLocalGameManager()
        {
            // Without a prefab, we can't properly spawn a networked object
            // Just create a local game manager - multiplayer will need a prefab to work properly
            if (Runner != null && Runner.IsServer)
            {
                // Check if there's already a NetworkGameManager in the scene
                var existingGM = FindObjectOfType<NetworkGameManager>();
                if (existingGM == null)
                {
                    GameObject gmObj = new GameObject("NetworkGameManager");
                    var gm = gmObj.AddComponent<NetworkGameManager>();
                    Debug.Log("[NetworkRunnerHandler] Created local NetworkGameManager (no prefab - multiplayer may not work correctly)");
                }
                else
                {
                    Debug.Log("[NetworkRunnerHandler] Using existing NetworkGameManager from scene");
                }
            }
        }

        #region INetworkRunnerCallbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"Player {player} joined");

            if (runner.IsServer)
            {
                // Spawn game manager if we have a valid prefab and haven't spawned one yet
                // Check if there's already a spawned (networked) game manager
                bool hasSpawnedGameManager = NetworkGameManager.Instance != null && 
                    NetworkGameManager.Instance.Object != null && 
                    NetworkGameManager.Instance.Object.IsValid;
                    
                if (!hasSpawnedGameManager && HasGameManagerPrefab())
                {
                    Debug.Log("[NetworkRunnerHandler] Spawning NetworkGameManager prefab");
                    SpawnGameManager(runner);
                }
                else if (!hasSpawnedGameManager)
                {
                    // Create game manager without prefab (for testing without prefabs)
                    Debug.Log("[NetworkRunnerHandler] Creating local NetworkGameManager (no prefab)");
                    CreateLocalGameManager();
                }

                // Spawn player object
                if (playerPrefab.IsValid)
                {
                    var playerObj = runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
                    spawnedPlayers[player] = playerObj;
                }
            }

            if (player == runner.LocalPlayer)
            {
                LocalPlayer = player;
            }

            OnPlayerJoinedEvent?.Invoke(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"Player {player} left");

            if (spawnedPlayers.TryGetValue(player, out var playerObj))
            {
                runner.Despawn(playerObj);
                spawnedPlayers.Remove(player);
            }

            OnPlayerLeftEvent?.Invoke(player);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            // Input handling if needed
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"Runner shutdown: {shutdownReason}");
            OnDisconnectedFromServerEvent?.Invoke();
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("Connected to server");
            OnConnectedToServerEvent?.Invoke();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"Disconnected from server: {reason}");
            OnDisconnectedFromServerEvent?.Invoke();
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            request.Accept();
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"Connect failed: {reason}");
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        #endregion
    }
}
