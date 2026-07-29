using HuesNCues.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// One row of the round score panel: a player's colour, name and the points they
    /// earned in the round that was just revealed.
    /// </summary>
    public class PlayerRoundScoreCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI scoreText;

        [Tooltip("Image tinted with the player's colour.")]
        [SerializeField] private Image colorImage;

        [Tooltip("{0} = points scored this round.")]
        [SerializeField] private string scoreFormat = "+{0}";

        [Header("Score colours")]
        [Tooltip("Used when the player scored at least one point.")]
        [SerializeField] private Color positiveScoreColor = new Color(0.35f, 0.85f, 0.45f);

        [Tooltip("Used when the player scored nothing this round.")]
        [SerializeField] private Color zeroScoreColor = new Color(0.65f, 0.65f, 0.7f);

        public void Set(string playerName, int colorIndex, int roundScore)
        {
            if (nameText != null) nameText.text = playerName;
            if (colorImage != null) colorImage.color = PlayerPalette.Get(colorIndex);

            if (scoreText != null)
            {
                scoreText.text = string.Format(scoreFormat, roundScore);
                scoreText.color = roundScore > 0 ? positiveScoreColor : zeroScoreColor;
            }
        }
    }
}
