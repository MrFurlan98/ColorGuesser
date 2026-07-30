using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorGuesser.Game
{
    /// <summary>The end-of-match statistics, already worked out.</summary>
    public struct MatchStatsInfo
    {
        public int Rounds;
        public int Players;

        /// <summary>The clue from the round that produced the most points.</summary>
        public string BestClue;

        /// <summary>The colour from the round that produced the fewest points.</summary>
        public string HardestColor;

        /// <summary>Guesses that landed exactly on the target, across the whole match.</summary>
        public int ExactGuesses;

        /// <summary>How long the match took.</summary>
        public float Seconds;
    }

    /// <summary>
    /// The stats panel on the end screen: a summary line plus four highlights from the
    /// match (best clue, hardest colour, exact guesses and total time).
    /// </summary>
    public class StatsPanelView : MonoBehaviour
    {
        [Tooltip("Summary line, e.g. \"8 rodadas • 6 jogadores\".")]
        [SerializeField] private TextMeshProUGUI summaryText;

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI bestClueText;
        [SerializeField] private TextMeshProUGUI hardestColorText;
        [SerializeField] private TextMeshProUGUI exactGuessesText;
        [SerializeField] private TextMeshProUGUI timeText;

        [Header("Buttons")]
        [Tooltip("Restarts the match with the same players (host only).")]
        [SerializeField] private Button playAgainButton;

        [Tooltip("Leaves the room and returns to the main menu.")]
        [SerializeField] private Button menuButton;

        [Header("Labels")]
        [Tooltip("{0} = rounds, {1} = players.")]
        [SerializeField] private string summaryFormat = "{0} rodadas • {1} jogadores";

        [Tooltip("Shown when a stat has no value yet (e.g. nobody scored).")]
        [SerializeField] private string emptyLabel = "--";

        /// <summary>Raised when the host restarts the match.</summary>
        public event Action PlayAgainClicked;

        /// <summary>Raised when the player leaves for the main menu.</summary>
        public event Action MenuClicked;

        private void Awake()
        {
            if (playAgainButton != null) playAgainButton.onClick.AddListener(() => PlayAgainClicked?.Invoke());
            if (menuButton != null) menuButton.onClick.AddListener(() => MenuClicked?.Invoke());
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        /// <summary>Only the host can restart, so the button is hidden for everyone else.</summary>
        public void SetPlayAgainVisible(bool visible)
        {
            if (playAgainButton != null && playAgainButton.gameObject.activeSelf != visible)
                playAgainButton.gameObject.SetActive(visible);
        }

        public void Show(MatchStatsInfo stats)
        {
            if (summaryText != null)
                summaryText.text = string.Format(summaryFormat, stats.Rounds, stats.Players);

            if (bestClueText != null)
                bestClueText.text = Or(stats.BestClue);
            if (hardestColorText != null)
                hardestColorText.text = Or(stats.HardestColor);
            if (exactGuessesText != null)
                exactGuessesText.text = stats.ExactGuesses.ToString();
            if (timeText != null)
                timeText.text = FormatTime(stats.Seconds);
        }

        private string Or(string value) => string.IsNullOrWhiteSpace(value) ? emptyLabel : value;

        /// <summary>mm:ss, or h:mm:ss for a very long match.</summary>
        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int secs = total % 60;
            return hours > 0 ? $"{hours}:{minutes:00}:{secs:00}" : $"{minutes:00}:{secs:00}";
        }
    }
}
