using UnityEngine;
using System.Collections;

namespace MLBShowdown.Dice
{
    public class DiceVisualizer : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Color diceColor = new Color(0.8f, 0.15f, 0.15f);
        [SerializeField] private Color highlightColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color numberColor = Color.white;
        
        [Header("Animation")]
        [SerializeField] private float spinSpeed = 720f;
        [SerializeField] private float bounceHeight = 0.5f;
        [SerializeField] private AnimationCurve bounceCurve;

        private Renderer diceRenderer;
        private TextMesh numberDisplay;
        private bool isAnimating;
        private int displayedNumber;

        void Awake()
        {
            diceRenderer = GetComponent<Renderer>();
            SetupNumberDisplay();
            
            if (bounceCurve == null || bounceCurve.keys.Length == 0)
            {
                bounceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            }
        }

        private void SetupNumberDisplay()
        {
            Transform existingDisplay = transform.Find("NumberDisplay");
            if (existingDisplay != null)
            {
                numberDisplay = existingDisplay.GetComponent<TextMesh>();
                return;
            }

            GameObject displayObj = new GameObject("NumberDisplay");
            displayObj.transform.SetParent(transform);
            displayObj.transform.localPosition = Vector3.up * 0.6f;
            
            numberDisplay = displayObj.AddComponent<TextMesh>();
            numberDisplay.fontSize = 48;
            numberDisplay.characterSize = 0.1f;
            numberDisplay.anchor = TextAnchor.MiddleCenter;
            numberDisplay.alignment = TextAlignment.Center;
            numberDisplay.color = numberColor;
            
            displayObj.AddComponent<FaceCamera>();
        }

        public void ShowRolling()
        {
            if (isAnimating) return;
            StartCoroutine(RollingAnimation());
        }

        public void ShowResult(int result)
        {
            StopAllCoroutines();
            isAnimating = false;
            displayedNumber = result;
            
            if (numberDisplay != null)
            {
                numberDisplay.text = result.ToString();
            }

            StartCoroutine(ResultAnimation(result));
        }

        private IEnumerator RollingAnimation()
        {
            isAnimating = true;
            float elapsed = 0f;
            
            while (isAnimating)
            {
                // Spin the dice
                transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime);
                transform.Rotate(Vector3.right * spinSpeed * 0.7f * Time.deltaTime);

                // Show random numbers
                if (numberDisplay != null)
                {
                    numberDisplay.text = Random.Range(1, 21).ToString();
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator ResultAnimation(int result)
        {
            // Flash highlight
            if (diceRenderer != null)
            {
                Material mat = diceRenderer.material;
                Color originalColor = mat.color;
                
                for (int i = 0; i < 3; i++)
                {
                    mat.color = highlightColor;
                    yield return new WaitForSeconds(0.1f);
                    mat.color = originalColor;
                    yield return new WaitForSeconds(0.1f);
                }
            }

            // Scale pop for emphasis on high/low rolls
            if (result == 1 || result == 20)
            {
                yield return StartCoroutine(ScalePop(1.3f));
            }
        }

        private IEnumerator ScalePop(float targetScale)
        {
            Vector3 originalScale = transform.localScale;
            Vector3 popScale = originalScale * targetScale;

            // Scale up
            float t = 0;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(originalScale, popScale, t / 0.15f);
                yield return null;
            }

            // Scale down with bounce
            t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = bounceCurve.Evaluate(t / 0.3f);
                transform.localScale = Vector3.Lerp(popScale, originalScale, progress);
                yield return null;
            }

            transform.localScale = originalScale;
        }

        public void SetColor(Color color)
        {
            diceColor = color;
            if (diceRenderer != null)
            {
                diceRenderer.material.color = color;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }
    }
}
