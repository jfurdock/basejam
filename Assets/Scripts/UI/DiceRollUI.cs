using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using MLBShowdown.Dice;

namespace MLBShowdown.UI
{
    public class DiceRollUI : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TextMeshProUGUI rollResultText;
        [SerializeField] private TextMeshProUGUI rollTypeText;
        [SerializeField] private Image diceIcon;
        [SerializeField] private Animator diceAnimator;

        [Header("Animation")]
        [SerializeField] private float displayDuration = 2f;
        [SerializeField] private float rollAnimationSpeed = 0.05f;
        [SerializeField] private int rollAnimationCycles = 10;

        [Header("Colors")]
        [SerializeField] private Color lowRollColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color midRollColor = new Color(1f, 1f, 1f);
        [SerializeField] private Color highRollColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.8f, 0f);

        private DiceRoller3D diceRoller;
        private Coroutine animationCoroutine;

        void Start()
        {
            diceRoller = FindObjectOfType<DiceRoller3D>();
            if (diceRoller != null)
            {
                diceRoller.OnDiceRollStarted += HandleRollStarted;
                diceRoller.OnDiceRollComplete += HandleRollComplete;
            }

            // Hide initially
            if (rollResultText != null) rollResultText.text = "";
            if (rollTypeText != null) rollTypeText.text = "";
        }

        void OnDestroy()
        {
            if (diceRoller != null)
            {
                diceRoller.OnDiceRollStarted -= HandleRollStarted;
                diceRoller.OnDiceRollComplete -= HandleRollComplete;
            }
        }

        private void HandleRollStarted()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }
            animationCoroutine = StartCoroutine(RollAnimation());
        }

        private void HandleRollComplete(int result)
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }
            
            DisplayResult(result);
        }

        private IEnumerator RollAnimation()
        {
            if (rollResultText == null) yield break;

            for (int i = 0; i < rollAnimationCycles; i++)
            {
                int randomNum = Random.Range(1, 21);
                rollResultText.text = randomNum.ToString();
                rollResultText.color = midRollColor;
                yield return new WaitForSeconds(rollAnimationSpeed);
            }
        }

        private void DisplayResult(int result)
        {
            if (rollResultText != null)
            {
                rollResultText.text = result.ToString();
                rollResultText.color = GetResultColor(result);

                // Scale animation for emphasis
                StartCoroutine(ScalePop(rollResultText.transform));
            }

            // Trigger animator if available
            if (diceAnimator != null)
            {
                diceAnimator.SetTrigger("ShowResult");
            }
        }

        private Color GetResultColor(int result)
        {
            if (result == 1) return lowRollColor;
            if (result == 20) return criticalColor;
            if (result <= 5) return Color.Lerp(lowRollColor, midRollColor, (result - 1) / 4f);
            if (result >= 16) return Color.Lerp(midRollColor, highRollColor, (result - 15) / 5f);
            return midRollColor;
        }

        private IEnumerator ScalePop(Transform target)
        {
            Vector3 originalScale = target.localScale;
            Vector3 popScale = originalScale * 1.3f;

            // Scale up
            float t = 0;
            while (t < 0.1f)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(originalScale, popScale, t / 0.1f);
                yield return null;
            }

            // Scale back down
            t = 0;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(popScale, originalScale, t / 0.2f);
                yield return null;
            }

            target.localScale = originalScale;
        }

        public void SetRollType(string rollType)
        {
            if (rollTypeText != null)
            {
                rollTypeText.text = rollType;
            }
        }

        public void ShowDefenseRoll()
        {
            SetRollType("DEFENSE ROLL");
        }

        public void ShowOffenseRoll()
        {
            SetRollType("OFFENSE ROLL");
        }

        public void ShowOptionalRoll(string actionName)
        {
            SetRollType(actionName.ToUpper());
        }

        public void Clear()
        {
            if (rollResultText != null) rollResultText.text = "";
            if (rollTypeText != null) rollTypeText.text = "";
        }
    }
}
