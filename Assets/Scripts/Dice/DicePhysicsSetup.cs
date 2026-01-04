using UnityEngine;

namespace MLBShowdown.Dice
{
    public class DicePhysicsSetup : MonoBehaviour
    {
        [Header("Table/Surface Settings")]
        [SerializeField] private Vector3 tableSize = new Vector3(4f, 0.2f, 4f);
        [SerializeField] private Vector3 tablePosition = new Vector3(0f, -1f, 0f);
        [SerializeField] private float wallHeight = 2f;
        [SerializeField] private float wallThickness = 0.1f;
        [SerializeField] private bool showVisuals = false;

        [Header("Physics Materials")]
        [SerializeField] private float tableBounce = 0.3f;
        [SerializeField] private float tableFriction = 0.6f;

        private GameObject diceTable;
        private PhysicsMaterial tableMaterial;

        void Awake()
        {
            CreatePhysicsMaterial();
            CreateDiceTable();
            CreateWalls();
        }

        private void CreatePhysicsMaterial()
        {
            tableMaterial = new PhysicsMaterial("TableMaterial");
            tableMaterial.bounciness = tableBounce;
            tableMaterial.dynamicFriction = tableFriction;
            tableMaterial.staticFriction = tableFriction;
            tableMaterial.bounceCombine = PhysicsMaterialCombine.Average;
            tableMaterial.frictionCombine = PhysicsMaterialCombine.Average;
        }

        private void CreateDiceTable()
        {
            diceTable = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diceTable.name = "DiceTable";
            diceTable.transform.SetParent(transform);
            diceTable.transform.localPosition = tablePosition;
            diceTable.transform.localScale = tableSize;

            // Apply material
            var collider = diceTable.GetComponent<Collider>();
            if (collider != null)
            {
                collider.material = tableMaterial;
            }

            // Visual styling - green felt like a card table
            var renderer = diceTable.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.1f, 0.4f, 0.2f);
                renderer.enabled = showVisuals;
            }

            // Make it static for physics optimization
            diceTable.isStatic = true;
        }

        private void CreateWalls()
        {
            float halfWidth = tableSize.x / 2f;
            float halfDepth = tableSize.z / 2f;
            float tableTop = tablePosition.y + tableSize.y / 2f;

            // Create 4 walls
            CreateWall("WallNorth", new Vector3(0, tableTop + wallHeight / 2f, halfDepth + wallThickness / 2f),
                new Vector3(tableSize.x + wallThickness * 2, wallHeight, wallThickness));
            
            CreateWall("WallSouth", new Vector3(0, tableTop + wallHeight / 2f, -halfDepth - wallThickness / 2f),
                new Vector3(tableSize.x + wallThickness * 2, wallHeight, wallThickness));
            
            CreateWall("WallEast", new Vector3(halfWidth + wallThickness / 2f, tableTop + wallHeight / 2f, 0),
                new Vector3(wallThickness, wallHeight, tableSize.z));
            
            CreateWall("WallWest", new Vector3(-halfWidth - wallThickness / 2f, tableTop + wallHeight / 2f, 0),
                new Vector3(wallThickness, wallHeight, tableSize.z));
        }

        private void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(transform);
            wall.transform.localPosition = position;
            wall.transform.localScale = scale;

            var collider = wall.GetComponent<Collider>();
            if (collider != null)
            {
                collider.material = tableMaterial;
            }

            // Make walls semi-transparent
            var renderer = wall.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.SetFloat("_Mode", 3); // Transparent mode
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                mat.color = new Color(0.3f, 0.2f, 0.1f, 0.3f);
                renderer.material = mat;
                renderer.enabled = showVisuals;
            }

            wall.isStatic = true;
        }

        public Vector3 GetTableCenter()
        {
            return tablePosition + Vector3.up * (tableSize.y / 2f + 0.5f);
        }

        public Bounds GetTableBounds()
        {
            return new Bounds(tablePosition, tableSize);
        }
    }
}
