using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorGuesser.Game
{
    /// <summary>
    /// The round countdown: mm:ss plus a slider that drains from full to empty for a
    /// sense of urgency. Applies to both the clue and the guessing phases.
    ///
    /// Split out of MatchHud because it is the only part that updates every frame.
    /// </summary>
    public class RoundTimerView : MonoBehaviour
    {
        [Tooltip("Countdown in mm:ss.")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Tooltip("Fills from 1 to 0 as the phase runs out.")]
        [SerializeField] private Slider timerSlider;

        [Tooltip("Container hidden when the phase is untimed. Empty = this GameObject.")]
        [SerializeField] private GameObject timerRoot;

        /// <summary>
        /// Shows the remaining time. A total of 0 (an untimed phase) hides the timer.
        /// </summary>
        public void SetTimer(float secondsLeft, float secondsTotal)
        {
            bool show = secondsTotal > 0f;
            var root = timerRoot != null ? timerRoot : gameObject;
            if (root.activeSelf != show) root.SetActive(show);
            if (!show) return;

            if (timerText != null)
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
                timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }
            if (timerSlider != null)
            {
                timerSlider.minValue = 0f;
                timerSlider.maxValue = 1f;
                timerSlider.value = Mathf.Clamp01(secondsLeft / secondsTotal);
            }
        }
    }
}
