using System.Collections;
using HuesNCues.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// One row of the round score panel: a player's colour, name, the points they earned
    /// in the round just revealed and their running total.
    ///
    /// When both score texts are assigned the points animate: the "+3" drifts across to
    /// the total and fades out while the total counts up to its new value.
    /// </summary>
    public class PlayerRoundScoreCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;

        [Tooltip("Points won this round. Coloured by whether the player scored.")]
        [SerializeField] private TextMeshProUGUI scoreText;

        [Tooltip("Optional separate text for the running total. Leave empty to append it " +
                 "to the round score instead (tinted with its own colour). Required for " +
                 "the count-up animation.")]
        [SerializeField] private TextMeshProUGUI totalText;

        [Tooltip("Image tinted with the player's colour.")]
        [SerializeField] private Image colorImage;

        [Header("Score colours")]
        [Tooltip("Used when the player scored at least one point.")]
        [SerializeField] private Color positiveScoreColor = new Color(0.35f, 0.85f, 0.45f);

        [Tooltip("Used when the player scored nothing this round.")]
        [SerializeField] private Color zeroScoreColor = new Color(0.65f, 0.65f, 0.7f);

        [Tooltip("Always this colour, whatever the player scored.")]
        [SerializeField] private Color totalScoreColor = new Color(0.75f, 0.78f, 0.85f);

        [Header("Formats")]
        [Tooltip("{0} = points won this round.")]
        [SerializeField] private string roundFormat = "+{0}";

        [Tooltip("{0} = the player's running total.")]
        [SerializeField] private string totalFormat = "({0})";

        [Tooltip("Placed between the two when they share one text object.")]
        [SerializeField] private string separator = " ";

        [Header("Animation")]
        [Tooltip("Fly the round score into the total and count the total up.")]
        [SerializeField] private bool animateScore = true;

        [Tooltip("Pause before the points fly across, so the numbers can be read first.")]
        [SerializeField] private float startDelay = 0.4f;

        [SerializeField] private float duration = 0.6f;

        [Tooltip("Layout group holding the score texts. It is switched off while the " +
                 "points fly across, otherwise the layout fights the animation. " +
                 "Auto-found from the score text's parent if left empty.")]
        [SerializeField] private LayoutGroup scoreLayout;

        private Vector2 _scoreHome;   // where the layout puts the round score
        private Coroutine _animation;

        /// <summary>
        /// Fills the row. The round score is coloured by whether the player scored; the
        /// running total always uses its own colour, so the two read as separate things.
        /// </summary>
        public void Set(string playerName, int colorIndex, int roundScore, int totalScore)
        {
            StopAnimation();
            RelayoutAndCacheHome(); // layout decides the positions, then we take over

            if (nameText != null) nameText.text = playerName;
            if (colorImage != null) colorImage.color = PlayerPalette.Get(colorIndex);

            string round = string.Format(roundFormat, roundScore);
            string total = string.Format(totalFormat, totalScore);
            Color roundColor = roundScore > 0 ? positiveScoreColor : zeroScoreColor;

            // Cards are pooled, so always restore the pre-animation look first.
            if (scoreText != null)
            {
                scoreText.rectTransform.anchoredPosition = _scoreHome;
                scoreText.alpha = 1f;
                scoreText.text = round;
                scoreText.color = roundColor;
            }

            if (totalText == null)
            {
                // One object: the base colour covers the round score and a rich-text tag
                // overrides it for the total. No animation is possible here.
                if (scoreText != null)
                    scoreText.text = $"{round}{separator}" +
                                     $"<color=#{ColorUtility.ToHtmlStringRGB(totalScoreColor)}>{total}</color>";
                return;
            }

            totalText.color = totalScoreColor;

            bool canAnimate = animateScore && scoreText != null && roundScore > 0 && isActiveAndEnabled;
            if (!canAnimate)
            {
                totalText.text = total;
                return;
            }

            // Start from the total BEFORE this round, then count up to it.
            totalText.text = string.Format(totalFormat, totalScore - roundScore);
            _animation = StartCoroutine(AnimateScore(roundScore, totalScore));
        }

        private IEnumerator AnimateScore(int roundScore, int totalScore)
        {
            yield return new WaitForSeconds(startDelay);

            // Only travel if both texts share a parent; otherwise just fade in place.
            bool sameParent = scoreText.rectTransform.parent == totalText.rectTransform.parent;
            Vector2 target = sameParent ? totalText.rectTransform.anchoredPosition : _scoreHome;

            // Hand control of the position over from the layout group to this animation.
            if (scoreLayout != null) scoreLayout.enabled = false;

            int from = totalScore - roundScore;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

                scoreText.rectTransform.anchoredPosition = Vector2.Lerp(_scoreHome, target, e);
                scoreText.alpha = 1f - e;
                totalText.text = string.Format(totalFormat, Mathf.RoundToInt(Mathf.Lerp(from, totalScore, e)));
                yield return null;
            }

            scoreText.alpha = 0f;
            totalText.text = string.Format(totalFormat, totalScore);
            _animation = null;
        }

        /// <summary>
        /// Turns the layout group back on and forces it to run, so the texts are back in
        /// their proper places after a previous animation moved them. The resulting
        /// position is what the next animation starts from.
        /// </summary>
        private void RelayoutAndCacheHome()
        {
            if (scoreText == null) return;

            if (scoreLayout == null && scoreText.rectTransform.parent != null)
                scoreLayout = scoreText.rectTransform.parent.GetComponent<LayoutGroup>();

            if (scoreLayout != null)
            {
                scoreLayout.enabled = true;
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)scoreLayout.transform);
            }

            _scoreHome = scoreText.rectTransform.anchoredPosition;
        }

        private void StopAnimation()
        {
            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;
            }
            // Never leave the card with its layout switched off.
            if (scoreLayout != null) scoreLayout.enabled = true;
        }

        private void OnDisable() => StopAnimation();
    }
}
