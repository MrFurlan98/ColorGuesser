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

        /// <summary>
        /// Shows the clue for the half of the round being played, e.g. “QUENTE”, or the
        /// placeholder while the cue master has not given it yet. The second clue replaces
        /// the first rather than joining it, so the board is never read against a word that
        /// belongs to guesses already locked in. visible = false outside a round.
        /// </summary>
        public void SetClue(string clue, bool visible)
        {
            var target = root != null ? root : (clueText != null ? clueText.gameObject : null);
            if (target != null && target.activeSelf != visible) target.SetActive(visible);
            if (!visible || clueText == null) return;

            clueText.text = string.IsNullOrWhiteSpace(clue) ? placeholder : Quote(clue);
        }

        private static string Quote(string word) => $"“{word.Trim().ToUpperInvariant()}”";
    }
}
