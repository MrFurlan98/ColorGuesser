using System.Collections;
using ColorGuesser.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorGuesser.Game
{
    /// <summary>
    /// One row of the round score panel: a player's colour, name, the points they earned
    /// in the round just revealed and their running total.
    ///
    /// The numbers count up rather than appearing at once: first the round score climbs
    /// from zero, then the running total climbs to its new value. Nothing moves or fades,
    /// so the effect is immune to how the row is laid out.
    /// </summary>
    public class PlayerRoundScoreCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;

        [Tooltip("Points won this round. Coloured by whether the player scored.")]
        [SerializeField] private TextMeshProUGUI scoreText;

        [Tooltip("Optional separate text for the running total. Leave empty to append it " +
                 "to the round score instead (tinted with its own colour).")]
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

        [Header("Count-up")]
        [SerializeField] private bool animateScore = true;

        [Tooltip("Pause before the numbers start climbing.")]
        [SerializeField] private float startDelay = 0.3f;

        [Tooltip("Seconds for the numbers to climb to their final values.")]
        [SerializeField] private float countDuration = 0.5f;

        private Coroutine _animation;

        // What this card is currently showing, so repeated updates with the same numbers
        // do not restart (or interrupt) the count-up.
        private string _shownName;
        private int _shownRound, _shownTotal;
        private bool _populated;

        /// <summary>
        /// Fills the row. The round score is coloured by whether the player scored; the
        /// running total always uses its own colour, so the two read as separate things.
        /// </summary>
        public void Set(string playerName, int colorIndex, int roundScore, int totalScore)
        {
            // The panel is refreshed on every state change (each incoming "next round"
            // vote, for instance). Re-running the count-up then would restart it - or,
            // with refreshes arriving faster than the start delay, stop it ever playing.
            if (_populated && roundScore == _shownRound && totalScore == _shownTotal &&
                playerName == _shownName)
                return;

            _shownName = playerName;
            _shownRound = roundScore;
            _shownTotal = totalScore;
            _populated = true;

            StopAnimation();

            if (nameText != null) nameText.text = playerName;
            if (colorImage != null) colorImage.color = PlayerPalette.Get(colorIndex);
            if (scoreText != null) scoreText.color = roundScore > 0 ? positiveScoreColor : zeroScoreColor;
            if (totalText != null) totalText.color = totalScoreColor;

            // Nothing to count when no points were won: the total has not moved either.
            bool canAnimate = animateScore && roundScore > 0 && isActiveAndEnabled;
            if (!canAnimate)
            {
                Render(roundScore, totalScore);
                return;
            }

            Render(0, totalScore - roundScore);   // start from before this round
            _animation = StartCoroutine(CountUp(roundScore, totalScore));
        }

        /// <summary>Both numbers climb together: the round score from zero, the total
        /// from where it stood before this round.</summary>
        private IEnumerator CountUp(int roundScore, int totalScore)
        {
            yield return new WaitForSeconds(startDelay);

            int totalBefore = totalScore - roundScore;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, countDuration);
                float e = Mathf.Clamp01(t);

                Render(Mathf.RoundToInt(Mathf.Lerp(0f, roundScore, e)),
                       Mathf.RoundToInt(Mathf.Lerp(totalBefore, totalScore, e)));
                yield return null;
            }

            Render(roundScore, totalScore);
            _animation = null;
        }

        /// <summary>Writes the two numbers, into one text object or two.</summary>
        private void Render(int roundValue, int totalValue)
        {
            string round = string.Format(roundFormat, roundValue);
            string total = string.Format(totalFormat, totalValue);

            if (totalText != null)
            {
                if (scoreText != null) scoreText.text = round;
                totalText.text = total;
                return;
            }

            // One object: the base colour covers the round score and a rich-text tag
            // gives the total its own colour.
            if (scoreText != null)
                scoreText.text = $"{round}{separator}" +
                                 $"<color=#{ColorUtility.ToHtmlStringRGB(totalScoreColor)}>{total}</color>";
        }

        private void StopAnimation()
        {
            if (_animation == null) return;
            StopCoroutine(_animation);
            _animation = null;
        }

        private void OnDisable()
        {
            StopAnimation();
            // The panel was closed, so the next reveal should count up again even if the
            // numbers happen to repeat.
            _populated = false;
        }
    }
}
