using UnityEngine;

namespace MLBShowdown.Dice
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class D20Mesh : MonoBehaviour
    {
        [SerializeField] private Material diceMaterial;
        [SerializeField] private float size = 0.5f;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            
            CreateIcosahedron();
            SetupMaterial();
        }

        private void CreateIcosahedron()
        {
            // Golden ratio
            float t = (1f + Mathf.Sqrt(5f)) / 2f;

            // Icosahedron vertices (normalized and scaled)
            Vector3[] baseVertices = new Vector3[]
            {
                new Vector3(-1,  t,  0).normalized * size,
                new Vector3( 1,  t,  0).normalized * size,
                new Vector3(-1, -t,  0).normalized * size,
                new Vector3( 1, -t,  0).normalized * size,
                new Vector3( 0, -1,  t).normalized * size,
                new Vector3( 0,  1,  t).normalized * size,
                new Vector3( 0, -1, -t).normalized * size,
                new Vector3( 0,  1, -t).normalized * size,
                new Vector3( t,  0, -1).normalized * size,
                new Vector3( t,  0,  1).normalized * size,
                new Vector3(-t,  0, -1).normalized * size,
                new Vector3(-t,  0,  1).normalized * size
            };

            // 20 triangular faces (indices into baseVertices)
            int[] faceIndices = new int[]
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

            // Create mesh with separate vertices per face for flat shading
            Vector3[] vertices = new Vector3[60]; // 20 faces * 3 vertices
            Vector3[] normals = new Vector3[60];
            Vector2[] uvs = new Vector2[60];
            int[] triangles = new int[60];

            for (int face = 0; face < 20; face++)
            {
                int i0 = faceIndices[face * 3];
                int i1 = faceIndices[face * 3 + 1];
                int i2 = faceIndices[face * 3 + 2];

                Vector3 v0 = baseVertices[i0];
                Vector3 v1 = baseVertices[i1];
                Vector3 v2 = baseVertices[i2];

                // Calculate face normal
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                int baseIndex = face * 3;
                vertices[baseIndex] = v0;
                vertices[baseIndex + 1] = v1;
                vertices[baseIndex + 2] = v2;

                normals[baseIndex] = normal;
                normals[baseIndex + 1] = normal;
                normals[baseIndex + 2] = normal;

                // Simple UV mapping
                uvs[baseIndex] = new Vector2(0.5f, 1f);
                uvs[baseIndex + 1] = new Vector2(0f, 0f);
                uvs[baseIndex + 2] = new Vector2(1f, 0f);

                triangles[baseIndex] = baseIndex;
                triangles[baseIndex + 1] = baseIndex + 1;
                triangles[baseIndex + 2] = baseIndex + 2;
            }

            Mesh mesh = new Mesh();
            mesh.name = "D20";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;
        }

        private void SetupMaterial()
        {
            if (diceMaterial != null)
            {
                meshRenderer.material = diceMaterial;
            }
            else
            {
                // Create default red dice material
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.8f, 0.1f, 0.1f);
                mat.SetFloat("_Metallic", 0.3f);
                mat.SetFloat("_Glossiness", 0.7f);
                meshRenderer.material = mat;
            }
        }

        public void SetNumber(int number)
        {
            // In a full implementation, this would display the number on the appropriate face
            // For now, we'll add a TextMesh child
            Transform existingText = transform.Find("NumberText");
            if (existingText != null)
            {
                existingText.GetComponent<TextMesh>().text = number.ToString();
                return;
            }

            GameObject textObj = new GameObject("NumberText");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = Vector3.up * (size + 0.1f);
            
            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = number.ToString();
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.08f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;

            textObj.AddComponent<FaceCamera>();
        }
    }
}
