using ColorGuesser.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorGuesser.Game
{
    /// <summary>
    /// One player entry in the lobby grid. Goes on the PlayerLobbyCard prefab: shows
    /// the player's nickname, their chosen colour, and their status
    /// ("Anfitrião" for the host, otherwise "Pronto" / "Não pronto").
    ///
    /// Purely a view - LobbyController fills it in via Set().
    /// </summary>
    public class PlayerLobbyCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Tooltip("Text inside the icon: the first two letters of the name, uppercase.")]
        [SerializeField] private TextMeshProUGUI initialsText;

        [Tooltip("Image tinted with the player's colour (avatar, swatch or background).")]
        [SerializeField] private Image colorImage;
        [SerializeField] private Image statusImage;

        [Header("Status labels")]
        [SerializeField] private string hostLabel = "Anfitrião";
        [SerializeField] private string readyLabel = "Pronto";
        [SerializeField] private string notReadyLabel = "Não pronto";

        [Header("Status colours")]
        [SerializeField] private Color hostColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color readyColor = new Color(0.35f, 0.85f, 0.45f);
        [SerializeField] private Color notReadyColor = new Color(0.8f, 0.8f, 0.8f);

        /// <summary>Fills the card in for one player.</summary>
        public void Set(string playerName, int colorIndex, bool isHost, bool isReady)
        {
            if (nameText != null) nameText.text = playerName;
            if (initialsText != null) initialsText.text = Initials(playerName);
            if (colorImage != null) colorImage.color = PlayerPalette.Get(colorIndex);

            // One status per state, so the label and its icon always agree.
            string label = isHost ? hostLabel : (isReady ? readyLabel : notReadyLabel);
            Color statusColor = isHost ? hostColor : (isReady ? readyColor : notReadyColor);
            SetStatus(label, statusColor);
        }

        /// <summary>
        /// Fills the card with an arbitrary status - lets the same prefab be reused
        /// outside the lobby (e.g. "Palpitou" / "Escolhendo" during a round).
        /// </summary>
        public void Set(string playerName, int colorIndex, string status, Color statusColor)
        {
            if (nameText != null) nameText.text = playerName;
            if (initialsText != null) initialsText.text = Initials(playerName);
            if (colorImage != null) colorImage.color = PlayerPalette.Get(colorIndex);
            SetStatus(status, statusColor);
        }

        private void SetStatus(string label, Color statusColor)
        {
            if (statusText != null)
            {
                statusText.text = label;
                statusText.color = statusColor;
            }
            if (statusImage != null) statusImage.color = statusColor;
        }

        /// <summary>The first two letters of the name, uppercase (e.g. "Vinicius" -> "VI").</summary>
        private static string Initials(string playerName)
        {
            string trimmed = (playerName ?? string.Empty).Trim();
            if (trimmed.Length == 0) return "?";
            return trimmed.Substring(0, Mathf.Min(2, trimmed.Length)).ToUpperInvariant();
        }
    }
}
