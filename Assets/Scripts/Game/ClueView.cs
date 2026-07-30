using TMPro;
using UnityEngine;

namespace ColorGuesser.Game
{
    /// <summary>
    /// The "Clue" block inside GameInfo: shows the round's clue words in uppercase
    /// between quotes, or a placeholder while the cue master has not given one yet.
    /// </summary>
    public class ClueView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI clueText;

        [Tooltip("Shown while there is no clue yet.")]
        [SerializeField] private string placeholder = "???";

        [Tooltip("Container to hide outside a round. Empty = only the text is hidden.")]
        [SerializeField] private GameObject root;

        /// <summary>Shows the clues, e.g. “QUENTE” “FOGO”. visible = false outside a round.</summary>
        public void SetClue(string clue1, string clue2, bool visible)
        {
            var target = root != null ? root : (clueText != null ? clueText.gameObject : null);
            if (target != null && target.activeSelf != visible) target.SetActive(visible);
            if (!visible || clueText == null) return;

            string text = string.IsNullOrWhiteSpace(clue1) ? placeholder : Quote(clue1);
            if (!string.IsNullOrWhiteSpace(clue2)) text += "   " + Quote(clue2);
            clueText.text = text;
        }

        private static string Quote(string word) => $"“{word.Trim().ToUpperInvariant()}”";
    }
}
