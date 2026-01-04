using System;
using System.Collections;
using UnityEngine;
using Fusion;
using Random = UnityEngine.Random;

namespace MLBShowdown.Dice
{
    public class DiceRoller3D : NetworkBehaviour
    {
        [Header("Dice Settings")]
        [SerializeField] private GameObject dicePrefab;
        [SerializeField] private float throwForce = 8f;
        [SerializeField] private float torqueForce = 300f;
        [SerializeField] private float settleTime = 2f;
        [SerializeField] private Transform diceSpawnPoint;
        [SerializeField] private Transform diceContainer;
        [SerializeField] private float diceScale = 0.6f;

        [Header("Physics")]
        [SerializeField] private PhysicsMaterial diceMaterial;
        [SerializeField] private float bounciness = 0.3f;
        [SerializeField] private float friction = 0.6f;
        
        [Header("Table Settings")]
        [SerializeField] private Vector3 tableSize = new Vector3(3f, 0.1f, 2f);
        [SerializeField] private Vector3 tableOffset = new Vector3(0, -1f, 2f);
        [SerializeField] private bool showTableVisuals = false;
        [SerializeField] private bool showDiceVisuals = false;

        [Networked] public int LastRollResult { get; set; }
        [Networked] public NetworkBool IsRolling { get; set; }

        public event Action<int> OnDiceRollComplete;
        public event Action OnDiceRollStarted;

        private GameObject currentDice;
        private Rigidbody diceRigidbody;
        private bool waitingForResult;
        private GameObject diceTable;
        private GameObject tableWalls;
        
        // Dice interaction
        private bool canInteract = false;
        private Vector3 lastMousePosition;
        private bool isDragging = false;

        public override void Spawned()
        {
            EnsureInitialized();
        }
        
        private void EnsureInitialized()
        {
            if (diceSpawnPoint == null)
            {
                GameObject spawnObj = new GameObject("DiceSpawnPoint");
                spawnObj.transform.SetParent(transform);
                spawnObj.transform.localPosition = new Vector3(0, 1.5f, 2f);
                diceSpawnPoint = spawnObj.transform;
            }

            if (diceContainer == null)
            {
                GameObject containerObj = new GameObject("DiceContainer");
                containerObj.transform.SetParent(transform);
                diceContainer = containerObj.transform;
            }
            
            // Create dice table and walls
            CreateDiceTable();
        }
        
        private void CreateDiceTable()
        {
            if (diceTable != null) return;
            
            // Create table surface
            diceTable = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diceTable.name = "DiceTable";
            diceTable.transform.SetParent(diceContainer);
            diceTable.transform.localPosition = tableOffset;
            diceTable.transform.localScale = tableSize;
            
            var tableRenderer = diceTable.GetComponent<Renderer>();
            if (tableRenderer != null)
            {
                tableRenderer.material.color = new Color(0.2f, 0.4f, 0.2f); // Green felt
                tableRenderer.enabled = showTableVisuals;
            }
            
            // Add physics material to table
            var tableCollider = diceTable.GetComponent<Collider>();
            if (tableCollider != null)
            {
                var tableMat = new PhysicsMaterial("TableMaterial");
                tableMat.bounciness = 0.2f;
                tableMat.dynamicFriction = 0.8f;
                tableMat.staticFriction = 0.8f;
                tableCollider.material = tableMat;
            }
            
            // Create invisible walls to keep dice on table
            CreateTableWalls();
        }
        
        private void CreateTableWalls()
        {
            tableWalls = new GameObject("TableWalls");
            tableWalls.transform.SetParent(diceContainer);
            tableWalls.transform.localPosition = tableOffset;
            
            float wallHeight = 0.5f;
            float wallThickness = 0.1f;
            
            // Front wall
            CreateWall(tableWalls.transform, "FrontWall", 
                new Vector3(0, wallHeight/2, -tableSize.z/2 - wallThickness/2),
                new Vector3(tableSize.x, wallHeight, wallThickness));
            
            // Back wall
            CreateWall(tableWalls.transform, "BackWall", 
                new Vector3(0, wallHeight/2, tableSize.z/2 + wallThickness/2),
                new Vector3(tableSize.x, wallHeight, wallThickness));
            
            // Left wall
            CreateWall(tableWalls.transform, "LeftWall", 
                new Vector3(-tableSize.x/2 - wallThickness/2, wallHeight/2, 0),
                new Vector3(wallThickness, wallHeight, tableSize.z));
            
            // Right wall
            CreateWall(tableWalls.transform, "RightWall", 
                new Vector3(tableSize.x/2 + wallThickness/2, wallHeight/2, 0),
                new Vector3(wallThickness, wallHeight, tableSize.z));
        }
        
        private void CreateWall(Transform parent, string name, Vector3 localPos, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = scale;
            
            // Make walls invisible but keep collider
            var renderer = wall.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }
        
        /// <summary>
        /// Enable dice interaction - call this when it's the player's turn
        /// </summary>
        public void EnableInteraction()
        {
            canInteract = true;
            
            // Create a dice ready to be thrown if none exists
            if (currentDice == null && !isRollingLocal)
            {
                CreateReadyDice();
            }
        }
        
        /// <summary>
        /// Disable dice interaction
        /// </summary>
        public void DisableInteraction()
        {
            canInteract = false;
        }
        
        private void CreateReadyDice()
        {
            EnsureInitialized();
            
            // Clean up previous dice
            if (currentDice != null)
            {
                Destroy(currentDice);
            }
            
            currentDice = CreateD20Dice();
            currentDice.transform.position = diceSpawnPoint.position;
            currentDice.transform.rotation = Random.rotation;
            
            // Make it kinematic until thrown
            diceRigidbody = currentDice.GetComponent<Rigidbody>();
            if (diceRigidbody != null)
            {
                diceRigidbody.isKinematic = true;
            }
        }
        
        private void Update()
        {
            // Rotate dice while waiting to be thrown
            if (canInteract && currentDice != null && !isRollingLocal)
            {
                currentDice.transform.Rotate(Vector3.up * 30f * Time.deltaTime);
                currentDice.transform.Rotate(Vector3.right * 20f * Time.deltaTime);
            }
        }
        
        /// <summary>
        /// Called when player clicks the dice or roll button to throw
        /// </summary>
        public void OnDiceClicked()
        {
            if (!canInteract || isRollingLocal) return;
            
            // Throw with random velocity
            Vector3 randomVelocity = new Vector3(
                Random.Range(-2f, 2f),
                Random.Range(1f, 3f),
                Random.Range(2f, 5f)
            );
            ThrowDice(randomVelocity);
        }
        
        private void ThrowDice(Vector3 velocity)
        {
            if (currentDice == null || isRollingLocal) return;
            
            isRollingLocal = true;
            canInteract = false;
            OnDiceRollStarted?.Invoke();
            
            // Enable physics
            if (diceRigidbody != null)
            {
                diceRigidbody.isKinematic = false;
                
                // Apply throw force
                Vector3 throwDir = new Vector3(velocity.x * 0.5f, -1f, velocity.z + throwForce).normalized;
                diceRigidbody.AddForce(throwDir * throwForce, ForceMode.Impulse);
                diceRigidbody.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);
            }
            
            // Determine result and complete after settle time
            int result = Random.Range(1, 21);
            StartCoroutine(CompleteRollAfterDelayLocal(result));
            
            // Also notify the game manager that a roll was requested
            var gameManager = MLBShowdown.Network.NetworkGameManager.Instance;
            if (gameManager != null)
            {
                // The result will be handled by OnDiceRollComplete event
                // which the game manager is already subscribed to
            }
        }

        /// <summary>
        /// Request a dice roll. Use this for local/CPU mode when NetworkBehaviour isn't initialized.
        /// </summary>
        public void RequestRollLocal(int sides = 20)
        {
            if (isRollingLocal) return;
            
            // Initialize spawn point and container if not set (Spawned() isn't called in local mode)
            EnsureInitialized();
            
            isRollingLocal = true;
            OnDiceRollStarted?.Invoke();
            SpawnAndThrowDice(sides);
            
            int result = Random.Range(1, sides + 1);
            StartCoroutine(CompleteRollAfterDelayLocal(result));
        }
        
        private bool isRollingLocal = false;
        
        private IEnumerator CompleteRollAfterDelayLocal(int result)
        {
            yield return new WaitForSeconds(settleTime);
            isRollingLocal = false;
            OnDiceRollComplete?.Invoke(result);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestRoll(int sides = 20)
        {
            if (IsRolling) return;
            
            IsRolling = true;
            RPC_StartRollVisual(sides);
            
            // Server determines the result
            int result = Random.Range(1, sides + 1);
            StartCoroutine(CompleteRollAfterDelay(result));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_StartRollVisual(int sides)
        {
            OnDiceRollStarted?.Invoke();
            SpawnAndThrowDice(sides);
        }

        private void SpawnAndThrowDice(int sides)
        {
            // Clean up previous dice
            if (currentDice != null)
            {
                Destroy(currentDice);
            }

            // Create dice
            if (dicePrefab != null)
            {
                currentDice = Instantiate(dicePrefab, diceSpawnPoint.position, Random.rotation, diceContainer);
            }
            else
            {
                currentDice = CreateDefaultD20();
            }

            foreach (var renderer in currentDice.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = showDiceVisuals;
            }

            diceRigidbody = currentDice.GetComponent<Rigidbody>();
            if (diceRigidbody == null)
            {
                diceRigidbody = currentDice.AddComponent<Rigidbody>();
            }

            // Configure physics
            diceRigidbody.mass = 1f;
            diceRigidbody.linearDamping = 0.5f;
            diceRigidbody.angularDamping = 0.5f;

            // Apply throw force and torque
            Vector3 throwDirection = new Vector3(
                Random.Range(-0.3f, 0.3f),
                -1f,
                Random.Range(-0.3f, 0.3f)
            ).normalized;

            diceRigidbody.AddForce(throwDirection * throwForce, ForceMode.Impulse);
            diceRigidbody.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);

            waitingForResult = true;
        }

        private GameObject CreateD20Dice()
        {
            // Create an icosahedron (20-sided die)
            GameObject dice = new GameObject("D20");
            dice.transform.SetParent(diceContainer);
            
            // Create the mesh
            MeshFilter meshFilter = dice.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = dice.AddComponent<MeshRenderer>();
            
            meshFilter.mesh = CreateIcosahedronMesh();
            
            // Create material
            Material diceMat = new Material(Shader.Find("Standard"));
            diceMat.color = new Color(0.8f, 0.15f, 0.15f); // Red dice
            diceMat.SetFloat("_Glossiness", 0.8f);
            diceMat.SetFloat("_Metallic", 0.1f);
            meshRenderer.material = diceMat;
            
            // Scale the dice
            dice.transform.localScale = Vector3.one * diceScale;
            
            // Add mesh collider for accurate physics
            MeshCollider meshCollider = dice.AddComponent<MeshCollider>();
            meshCollider.convex = true;
            meshCollider.sharedMesh = meshFilter.mesh;
            
            // Add physics material
            if (diceMaterial != null)
            {
                meshCollider.material = diceMaterial;
            }
            else
            {
                var physMat = new PhysicsMaterial("DiceMaterial");
                physMat.bounciness = bounciness;
                physMat.dynamicFriction = friction;
                physMat.staticFriction = friction;
                physMat.bounceCombine = PhysicsMaterialCombine.Average;
                meshCollider.material = physMat;
            }
            
            // Add rigidbody
            Rigidbody rb = dice.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.linearDamping = 0.3f;
            rb.angularDamping = 0.3f;
            
            return dice;
        }
        
        private Mesh CreateIcosahedronMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Icosahedron";
            
            // Golden ratio
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            
            // Vertices of icosahedron
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-1,  t,  0).normalized,
                new Vector3( 1,  t,  0).normalized,
                new Vector3(-1, -t,  0).normalized,
                new Vector3( 1, -t,  0).normalized,
                new Vector3( 0, -1,  t).normalized,
                new Vector3( 0,  1,  t).normalized,
                new Vector3( 0, -1, -t).normalized,
                new Vector3( 0,  1, -t).normalized,
                new Vector3( t,  0, -1).normalized,
                new Vector3( t,  0,  1).normalized,
                new Vector3(-t,  0, -1).normalized,
                new Vector3(-t,  0,  1).normalized
            };
            
            // Triangles (20 faces)
            int[] triangles = new int[]
            {
                0, 11, 5,
                0, 5, 1,
                0, 1, 7,
                0, 7, 10,
                0, 10, 11,
                1, 5, 9,
                5, 11, 4,
                11, 10, 2,
                10, 7, 6,
                7, 1, 8,
                3, 9, 4,
                3, 4, 2,
                3, 2, 6,
                3, 6, 8,
                3, 8, 9,
                4, 9, 5,
                2, 4, 11,
                6, 2, 10,
                8, 6, 7,
                9, 8, 1
            };
            
            // Create expanded vertices for flat shading (each face needs its own vertices)
            Vector3[] expandedVertices = new Vector3[triangles.Length];
            int[] expandedTriangles = new int[triangles.Length];
            
            for (int i = 0; i < triangles.Length; i++)
            {
                expandedVertices[i] = vertices[triangles[i]];
                expandedTriangles[i] = i;
            }
            
            mesh.vertices = expandedVertices;
            mesh.triangles = expandedTriangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        private GameObject CreateDefaultD20()
        {
            return CreateD20Dice();
        }

        private IEnumerator CompleteRollAfterDelay(int result)
        {
            yield return new WaitForSeconds(settleTime);

            LastRollResult = result;
            IsRolling = false;
            
            RPC_BroadcastResult(result);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BroadcastResult(int result)
        {
            waitingForResult = false;
            
            // Update dice visual to show result
            if (currentDice != null)
            {
                ShowDiceResult(result);
            }

            OnDiceRollComplete?.Invoke(result);
        }

        private void ShowDiceResult(int result)
        {
            // Stop dice physics
            if (diceRigidbody != null)
            {
                diceRigidbody.isKinematic = true;
            }

            // Add text mesh to show result
            var textObj = new GameObject("ResultText");
            textObj.transform.SetParent(currentDice.transform);
            textObj.transform.localPosition = Vector3.up * 0.6f;
            
            var textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = result.ToString();
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.1f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;

            // Make text face camera
            textObj.AddComponent<FaceCamera>();
        }

        public void ClearDice()
        {
            if (currentDice != null)
            {
                Destroy(currentDice);
                currentDice = null;
            }
        }

        // For local/offline testing
        public int RollLocal(int sides = 20)
        {
            int result = Random.Range(1, sides + 1);
            OnDiceRollComplete?.Invoke(result);
            return result;
        }
    }

    public class FaceCamera : MonoBehaviour
    {
        private Camera mainCamera;

        void Start()
        {
            mainCamera = Camera.main;
        }

        void LateUpdate()
        {
            if (mainCamera != null)
            {
                transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                    mainCamera.transform.rotation * Vector3.up);
            }
        }
    }
}
