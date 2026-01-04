using UnityEngine;
using UnityEditor;
using Fusion;
using MLBShowdown.Network;

namespace MLBShowdown.Editor
{
    public class NetworkPrefabSetup : EditorWindow
    {
        [MenuItem("MLB Showdown/Setup Network Prefabs")]
        public static void SetupPrefabs()
        {
            // Create Resources folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            // Create NetworkGameManager prefab
            CreateNetworkGameManagerPrefab();
            
            Debug.Log("Network prefabs created in Assets/Resources!");
            EditorUtility.DisplayDialog("Setup Complete", 
                "Network prefabs created in Assets/Resources!\n\n" +
                "The prefab will be automatically loaded at runtime.\n" +
                "No manual assignment needed!", 
                "OK");
        }

        private static void CreateNetworkGameManagerPrefab()
        {
            string prefabPath = "Assets/Resources/NetworkGameManager.prefab";
            
            // Check if prefab already exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log("NetworkGameManager prefab already exists at " + prefabPath);
                return;
            }

            // Create a new GameObject
            GameObject gmObj = new GameObject("NetworkGameManager");
            
            // Add required components
            gmObj.AddComponent<NetworkObject>();
            gmObj.AddComponent<NetworkGameManager>();
            
            // Create the prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(gmObj, prefabPath);
            
            // Clean up the temporary object
            DestroyImmediate(gmObj);
            
            Debug.Log("Created NetworkGameManager prefab at " + prefabPath);
            
            // Select the prefab in the project window
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
    }
}
