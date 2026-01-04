using UnityEngine;
using UnityEngine.UI;
using MLBShowdown.Core;
using MLBShowdown.BaseRunning;

namespace MLBShowdown.UI
{
    public class BaseRunnerDisplay : MonoBehaviour
    {
        [Header("Base Indicators")]
        [SerializeField] private Image firstBaseImage;
        [SerializeField] private Image secondBaseImage;
        [SerializeField] private Image thirdBaseImage;
        [SerializeField] private Image homeBaseImage;

        [Header("Colors")]
        [SerializeField] private Color emptyColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private Color occupiedColor = new Color(1f, 0.8f, 0f, 1f);
        [SerializeField] private Color homeColor = new Color(1f, 1f, 1f, 1f);

        [Header("Diamond Layout")]
        [SerializeField] private RectTransform diamondContainer;
        [SerializeField] private float baseSize = 40f;
        [SerializeField] private float diamondSize = 120f;

        private BaseRunnerController baseRunnerController;

        void Start()
        {
            if (diamondContainer != null && firstBaseImage == null)
            {
                CreateDiamondLayout();
            }
        }

        void Update()
        {
            if (baseRunnerController == null)
            {
                baseRunnerController = FindObjectOfType<BaseRunnerController>();
            }

            if (baseRunnerController != null)
            {
                UpdateDisplay();
            }
        }

        private void CreateDiamondLayout()
        {
            // Create base images in diamond formation
            homeBaseImage = CreateBaseImage("Home", new Vector2(0, -diamondSize / 2));
            firstBaseImage = CreateBaseImage("First", new Vector2(diamondSize / 2, 0));
            secondBaseImage = CreateBaseImage("Second", new Vector2(0, diamondSize / 2));
            thirdBaseImage = CreateBaseImage("Third", new Vector2(-diamondSize / 2, 0));

            // Rotate bases 45 degrees to look like diamonds
            if (firstBaseImage != null) firstBaseImage.rectTransform.rotation = Quaternion.Euler(0, 0, 45);
            if (secondBaseImage != null) secondBaseImage.rectTransform.rotation = Quaternion.Euler(0, 0, 45);
            if (thirdBaseImage != null) thirdBaseImage.rectTransform.rotation = Quaternion.Euler(0, 0, 45);

            // Home plate is pentagon shape (we'll use a different rotation)
            if (homeBaseImage != null) homeBaseImage.rectTransform.rotation = Quaternion.Euler(0, 0, 0);

            // Draw base paths
            CreateBasePath(homeBaseImage.rectTransform.anchoredPosition, firstBaseImage.rectTransform.anchoredPosition);
            CreateBasePath(firstBaseImage.rectTransform.anchoredPosition, secondBaseImage.rectTransform.anchoredPosition);
            CreateBasePath(secondBaseImage.rectTransform.anchoredPosition, thirdBaseImage.rectTransform.anchoredPosition);
            CreateBasePath(thirdBaseImage.rectTransform.anchoredPosition, homeBaseImage.rectTransform.anchoredPosition);
        }

        private Image CreateBaseImage(string baseName, Vector2 position)
        {
            GameObject baseObj = new GameObject(baseName + "Base");
            baseObj.transform.SetParent(diamondContainer);
            
            RectTransform rect = baseObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(baseSize, baseSize);

            Image img = baseObj.AddComponent<Image>();
            img.color = baseName == "Home" ? homeColor : emptyColor;

            return img;
        }

        private void CreateBasePath(Vector2 from, Vector2 to)
        {
            GameObject pathObj = new GameObject("BasePath");
            pathObj.transform.SetParent(diamondContainer);
            pathObj.transform.SetAsFirstSibling(); // Put behind bases

            RectTransform rect = pathObj.AddComponent<RectTransform>();
            
            // Position at midpoint
            rect.anchoredPosition = (from + to) / 2f;
            
            // Calculate length and rotation
            float length = Vector2.Distance(from, to);
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            
            rect.sizeDelta = new Vector2(length, 3f);
            rect.rotation = Quaternion.Euler(0, 0, angle);

            Image img = pathObj.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.3f);
        }

        private void UpdateDisplay()
        {
            if (firstBaseImage != null)
                firstBaseImage.color = baseRunnerController.HasRunnerOn(Base.First) ? occupiedColor : emptyColor;
            
            if (secondBaseImage != null)
                secondBaseImage.color = baseRunnerController.HasRunnerOn(Base.Second) ? occupiedColor : emptyColor;
            
            if (thirdBaseImage != null)
                thirdBaseImage.color = baseRunnerController.HasRunnerOn(Base.Third) ? occupiedColor : emptyColor;
        }

        public void SetBaseRunnerController(BaseRunnerController controller)
        {
            baseRunnerController = controller;
        }
    }
}
