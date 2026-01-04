using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace MLBShowdown.Dice
{
    public class DiceRollerUI : MonoBehaviour
    {
        [Header("Dice Settings")]
        [SerializeField] private float diceScale = 0.5f;
        [SerializeField] private float throwForce = 5f;
        [SerializeField] private float torqueForce = 300f;
        [SerializeField] private float settleTime = 2.0f;
        
        [Header("Physics")]
        [SerializeField] private float bounciness = 0.4f;
        [SerializeField] private float friction = 0.5f;
        
        [Header("Render Settings")]
        [SerializeField] private int renderTextureSize = 512;
        [SerializeField] private Vector3 tableSize = new Vector3(3f, 0.1f, 2f);
        
        public event Action OnDiceRollStarted;
        public event Action<int> OnDiceRollComplete;
        
        private GameObject currentDice;
        private Rigidbody diceRigidbody;
        private GameObject diceSceneRoot;
        private Camera diceCamera;
        private RenderTexture diceRenderTexture;
        private RawImage diceDisplayImage;
        private GameObject diceDisplayPanel;
        
        private bool isRolling = false;
        private bool isInitialized = false;
        
        public bool IsRolling => isRolling;
        
        void Start()
        {
            Initialize();
        }
        
        public void Initialize()
        {
            if (isInitialized) return;
            CreateDiceScene();
            CreateUIDisplay();
            isInitialized = true;
        }
        
        private void CreateDiceScene()
        {
            diceSceneRoot = new GameObject("DiceScene");
            diceSceneRoot.transform.position = new Vector3(500, 500, 500);
            
            diceRenderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 24);
            diceRenderTexture.antiAliasing = 4;
            
            GameObject camObj = new GameObject("DiceCamera");
            camObj.transform.SetParent(diceSceneRoot.transform);
            camObj.transform.localPosition = new Vector3(0, 2.5f, -2f);
            camObj.transform.localRotation = Quaternion.Euler(45, 0, 0);
            
            diceCamera = camObj.AddComponent<Camera>();
            diceCamera.clearFlags = CameraClearFlags.SolidColor;
            diceCamera.backgroundColor = new Color(0.1f, 0.3f, 0.1f, 1f);
            diceCamera.targetTexture = diceRenderTexture;
            diceCamera.fieldOfView = 60f;
            diceCamera.nearClipPlane = 0.1f;
            diceCamera.farClipPlane = 100f;
            diceCamera.depth = 100;
            
            GameObject lightObj = new GameObject("DiceLight");
            lightObj.transform.SetParent(diceSceneRoot.transform);
            lightObj.transform.localPosition = new Vector3(0, 5, -2);
            lightObj.transform.localRotation = Quaternion.Euler(50, 0, 0);
            Light diceLight = lightObj.AddComponent<Light>();
            diceLight.type = LightType.Directional;
            diceLight.intensity = 1.5f;
            
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "DiceTable";
            table.transform.SetParent(diceSceneRoot.transform);
            table.transform.localPosition = Vector3.zero;
            table.transform.localScale = tableSize;
            var tableRenderer = table.GetComponent<Renderer>();
            Material tableMat = new Material(Shader.Find("Standard"));
            tableMat.color = new Color(0.15f, 0.4f, 0.15f);
            tableRenderer.material = tableMat;
            
            PhysicsMaterial tablePM = new PhysicsMaterial();
            tablePM.bounciness = 0.3f;
            tablePM.dynamicFriction = 0.6f;
            table.GetComponent<Collider>().material = tablePM;
            
            CreateWall("Front", new Vector3(0, 0.3f, -tableSize.z/2 - 0.05f), new Vector3(tableSize.x, 0.6f, 0.1f));
            CreateWall("Back", new Vector3(0, 0.3f, tableSize.z/2 + 0.05f), new Vector3(tableSize.x, 0.6f, 0.1f));
            CreateWall("Left", new Vector3(-tableSize.x/2 - 0.05f, 0.3f, 0), new Vector3(0.1f, 0.6f, tableSize.z));
            CreateWall("Right", new Vector3(tableSize.x/2 + 0.05f, 0.3f, 0), new Vector3(0.1f, 0.6f, tableSize.z));
            
            diceSceneRoot.SetActive(false);
        }
        
        private void CreateWall(string name, Vector3 pos, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall_" + name;
            wall.transform.SetParent(diceSceneRoot.transform);
            wall.transform.localPosition = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material.color = new Color(0.3f, 0.2f, 0.1f);
        }
        
        private void CreateUIDisplay()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            
            diceDisplayPanel = new GameObject("DiceDisplayPanel");
            diceDisplayPanel.transform.SetParent(canvas.transform);
            
            RectTransform rect = diceDisplayPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.25f, 0.25f);
            rect.anchorMax = new Vector2(0.75f, 0.75f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            
            diceDisplayImage = diceDisplayPanel.AddComponent<RawImage>();
            diceDisplayImage.texture = diceRenderTexture;
            diceDisplayImage.raycastTarget = false;
            
            diceDisplayPanel.SetActive(false);
        }
        
        public void RollDice()
        {
            if (isRolling) return;
            StartCoroutine(PerformDiceRoll());
        }
        
        private IEnumerator PerformDiceRoll()
        {
            isRolling = true;
            OnDiceRollStarted?.Invoke();
            
            diceSceneRoot.SetActive(true);
            diceDisplayPanel.SetActive(true);
            
            if (currentDice != null) Destroy(currentDice);
            
            currentDice = CreateDice();
            currentDice.transform.SetParent(diceSceneRoot.transform);
            currentDice.transform.localPosition = new Vector3(Random.Range(-0.3f, 0.3f), 1.5f, Random.Range(-0.2f, 0.2f));
            currentDice.transform.localRotation = Random.rotation;
            
            diceRigidbody = currentDice.GetComponent<Rigidbody>();
            Vector3 throwDir = new Vector3(Random.Range(-0.3f, 0.3f), -1f, Random.Range(-0.2f, 0.2f)).normalized;
            diceRigidbody.AddForce(throwDir * throwForce, ForceMode.Impulse);
            diceRigidbody.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);
            
            yield return new WaitForSeconds(settleTime);
            
            int result = Random.Range(1, 21);
            if (diceRigidbody != null) diceRigidbody.isKinematic = true;
            
            yield return new WaitForSeconds(0.8f);
            
            isRolling = false;
            OnDiceRollComplete?.Invoke(result);
            
            yield return new WaitForSeconds(0.3f);
            
            diceDisplayPanel.SetActive(false);
            diceSceneRoot.SetActive(false);
            if (currentDice != null) { Destroy(currentDice); currentDice = null; }
        }
        
        private GameObject CreateDice()
        {
            GameObject dice = new GameObject("D20");
            MeshFilter mf = dice.AddComponent<MeshFilter>();
            MeshRenderer mr = dice.AddComponent<MeshRenderer>();
            mf.mesh = CreateIcosahedronMesh();
            
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.85f, 0.1f, 0.1f);
            mat.SetFloat("_Glossiness", 0.85f);
            mat.SetFloat("_Metallic", 0.1f);
            mr.material = mat;
            
            dice.transform.localScale = Vector3.one * diceScale;
            
            MeshCollider mc = dice.AddComponent<MeshCollider>();
            mc.convex = true;
            mc.sharedMesh = mf.mesh;
            
            PhysicsMaterial pm = new PhysicsMaterial();
            pm.bounciness = bounciness;
            pm.dynamicFriction = friction;
            mc.material = pm;
            
            Rigidbody rb = dice.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.linearDamping = 0.3f;
            rb.angularDamping = 0.3f;
            
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
        
        void OnDestroy()
        {
            if (diceRenderTexture != null) { diceRenderTexture.Release(); Destroy(diceRenderTexture); }
            if (currentDice != null) Destroy(currentDice);
            if (diceSceneRoot != null) Destroy(diceSceneRoot);
            if (diceDisplayPanel != null) Destroy(diceDisplayPanel);
        }
    }
}
