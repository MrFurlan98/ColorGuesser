using ColorGuesser.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorGuesser.Game
{
    /// <summary>
    /// One row of the final scoreboard: finishing position, the player's colour and
    /// initials, their name and their total score. The top three positions get their
    /// own medal colours.
    /// </summary>
    public class PlayerFinalScoreCard : MonoBehaviour
    {
        [Header("Position")]
        [Tooltip("The circle behind the position number.")]
        [SerializeField] private Image positionImage;
        [SerializeField] private TextMeshProUGUI positionText;

        [Header("Player")]
        [Tooltip("Icon tinted with the player's colour.")]
        [SerializeField] private Image iconImage;

        [Tooltip("The first two letters of the name, uppercase.")]
        [SerializeField] private TextMeshProUGUI initialsText;

        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI scoreText;

        [Header("Medal colours (1st, 2nd, 3rd)")]
        [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0.25f);
        [SerializeField] private Color silverColor = new Color(0.78f, 0.80f, 0.84f);
        [SerializeField] private Color bronzeColor = new Color(0.80f, 0.52f, 0.25f);

        [Tooltip("Used from 4th place onwards.")]
        [SerializeField] private Color defaultPositionColor = new Color(0.30f, 0.32f, 0.38f);

        [Tooltip("{0} = final score.")]
        [SerializeField] private string scoreFormat = "{0}";

        /// <summary>Fills the row. Position is 1-based; tied players share a position.</summary>
        public void Set(int position, string playerName, int colorIndex, int score)
        {
            if (positionText != null) positionText.text = position.ToString();
            if (positionImage != null) positionImage.color = MedalColor(position);

            if (iconImage != null) iconImage.color = PlayerPalette.Get(colorIndex);
            if (initialsText != null) initialsText.text = Initials(playerName);
            if (nameText != null) nameText.text = playerName;
            if (scoreText != null) scoreText.text = string.Format(scoreFormat, score);
        }

        private Color MedalColor(int position)
        {
            switch (position)
            {
                case 1: return goldColor;
                case 2: return silverColor;
                case 3: return bronzeColor;
                default: return defaultPositionColor;
            }
        }

        private static string Initials(string playerName)
        {
            string trimmed = (playerName ?? string.Empty).Trim();
            if (trimmed.Length == 0) return "?";
            return trimmed.Substring(0, Mathf.Min(2, trimmed.Length)).ToUpperInvariant();
        }
    }
}
