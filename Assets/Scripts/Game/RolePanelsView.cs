using UnityEngine;

namespace HuesNCues.Game
{
    /// <summary>
    /// Swaps the panel inside GameInfo according to this player's role in the round:
    /// the cue master gets the clue panel (write a clue), everyone else gets the guess
    /// panel (pick a colour). Both hide outside the playing phases, e.g. during the
    /// reveal and at the end of the match.
    /// </summary>
    public class RolePanelsView : MonoBehaviour
    {
        [Tooltip("Shown to the cue master: the clue input and its confirm button.")]
        [SerializeField] private GameObject cluePanel;

        [Tooltip("Shown to everyone else: the guessing prompt.")]
        [SerializeField] private GameObject guessPanel;

        /// <summary>
        /// Shows the panel matching the player's role. Pass visible = false to hide both
        /// (reveal / end of match / no round running).
        /// </summary>
        public void SetRole(bool isCueMaster, bool visible)
        {
            Show(cluePanel, visible && isCueMaster);
            Show(guessPanel, visible && !isCueMaster);
        }

        private static void Show(GameObject go, bool visible)
        {
            if (go != null && go.activeSelf != visible) go.SetActive(visible);
        }
    }
}
