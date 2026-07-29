using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>One guesser's state, handed to the view (no game types here).</summary>
    public struct GuessStatusInfo
    {
        public string Name;
        public int ColorIndex;
        public bool HasGuessed;
    }

    /// <summary>
    /// The "Guess" panel shown to everyone except the cue master: a confirm button for
    /// the cell you picked, the list of players and whether they have guessed, and how
    /// many are still deciding.
    /// </summary>
    public class GuessPanelView : MonoBehaviour
    {
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI remainingText;

        [Header("Player toggles — one per palette colour")]
        [Tooltip("Only the toggles whose colour a player picked are shown; a toggle " +
                 "switches on once that player has guessed.")]
        [SerializeField] private ToggleColorController[] playerToggles;

        [Header("Labels")]
        [Tooltip("{0} = players who confirmed, {1} = total guessers.")]
        [SerializeField] private string confirmedFormat = "{0}/{1} Confirmaram";

        /// <summary>Raised when the player confirms the cell they picked.</summary>
        public event Action ConfirmClicked;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(() => ConfirmClicked?.Invoke());
        }

        /// <summary>Only enabled once a cell is picked and this player has not guessed yet.</summary>
        public void SetConfirmEnabled(bool enabled)
        {
            if (confirmButton != null) confirmButton.interactable = enabled;
        }


        /// <summary>
        /// Shows one toggle per guesser, matched by the colour they chose: toggles for
        /// colours nobody picked are hidden, and a toggle switches on once that player
        /// has guessed. Also updates the "still to guess" counter.
        /// </summary>
        public void SetPlayers(IList<GuessStatusInfo> players)
        {
            int confirmed = 0;
            foreach (var p in players) if (p.HasGuessed) confirmed++;

            if (remainingText != null)
                remainingText.text = string.Format(confirmedFormat, confirmed, players.Count);

            if (playerToggles == null) return;

            foreach (var toggle in playerToggles)
            {
                if (toggle == null) continue;

                bool inPlay = false, hasGuessed = false;
                foreach (var p in players)
                    if (p.ColorIndex == toggle.ColorIndex)
                    {
                        inPlay = true;
                        hasGuessed = p.HasGuessed;
                        break;
                    }

                if (toggle.gameObject.activeSelf != inPlay) toggle.gameObject.SetActive(inPlay);
                if (inPlay) toggle.SetOn(hasGuessed);
            }
        }
    }
}
