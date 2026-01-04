using UnityEngine;

namespace MLBShowdown.Core
{
    /// <summary>
    /// Enforces a landscape phone aspect ratio for the game.
    /// Attach to a GameObject in the scene or it will be created automatically.
    /// </summary>
    public class AspectRatioEnforcer : MonoBehaviour
    {
        [SerializeField] private float targetAspectWidth = 19.5f;
        [SerializeField] private float targetAspectHeight = 9f;
        
        private static AspectRatioEnforcer instance;
        
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            EnforceAspectRatio();
        }

        void Start()
        {
            EnforceAspectRatio();
        }

        private void EnforceAspectRatio()
        {
            float targetAspect = targetAspectWidth / targetAspectHeight;
            float windowAspect = (float)Screen.width / Screen.height;
            float scaleHeight = windowAspect / targetAspect;

            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            if (scaleHeight < 1.0f)
            {
                // Letterbox (black bars on top/bottom)
                Rect rect = mainCamera.rect;
                rect.width = 1.0f;
                rect.height = scaleHeight;
                rect.x = 0;
                rect.y = (1.0f - scaleHeight) / 2.0f;
                mainCamera.rect = rect;
            }
            else
            {
                // Pillarbox (black bars on sides)
                float scaleWidth = 1.0f / scaleHeight;
                Rect rect = mainCamera.rect;
                rect.width = scaleWidth;
                rect.height = 1.0f;
                rect.x = (1.0f - scaleWidth) / 2.0f;
                rect.y = 0;
                mainCamera.rect = rect;
            }

#if UNITY_EDITOR
            // In editor, also try to set the game view resolution
            SetEditorResolution();
#endif
        }

#if UNITY_EDITOR
        private void SetEditorResolution()
        {
            // This sets a suggested resolution for testing
            // The actual Game view resolution is controlled by the Game view dropdown
            int targetWidth = 1920;
            int targetHeight = (int)(targetWidth / (targetAspectWidth / targetAspectHeight));
            
            // Log the recommended resolution
            Debug.Log($"[AspectRatioEnforcer] Recommended resolution: {targetWidth}x{targetHeight} ({targetAspectWidth}:{targetAspectHeight})");
        }
#endif

        void OnValidate()
        {
            if (Application.isPlaying)
            {
                EnforceAspectRatio();
            }
        }
    }
}
