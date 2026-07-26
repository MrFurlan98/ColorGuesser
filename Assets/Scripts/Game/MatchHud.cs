using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// Passive view (Humble Object / MVP pattern) for the match HUD. It holds
    /// references to the UI widgets and exposes plain methods/events. It contains
    /// NO game rules and does not know about MatchController - MatchView drives it.
    ///
    /// This lives on the MatchHud prefab so all layout and sprites can be polished in
    /// the editor without changing code. Create the prefab with
    /// Tools > Hues N Cues > Create MatchHud Prefab.
    /// </summary>
    public class MatchHud : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image secretSwatch;      // toggling this hides its label child too
        [SerializeField] private TMP_InputField clueInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI nextLabel;
        [SerializeField] private TextMeshProUGUI scoreboardText;

        /// <summary>Raised when the cue master submits a clue (button or Enter key).</summary>
        public event Action SubmitClueRequested;

        /// <summary>Raised when the Next / Play Again button is pressed.</summary>
        public event Action NextRequested;

        private void Awake()
        {
            if (submitButton != null) submitButton.onClick.AddListener(() => SubmitClueRequested?.Invoke());
            if (nextButton != null) nextButton.onClick.AddListener(() => NextRequested?.Invoke());
            if (clueInput != null) clueInput.onSubmit.AddListener(_ => SubmitClueRequested?.Invoke());
        }

        public string ClueText
        {
            get => clueInput != null ? clueInput.text : string.Empty;
            set { if (clueInput != null) clueInput.text = value; }
        }

        public void SetStatus(string text) { if (statusText != null) statusText.text = text; }
        public void SetScoreboard(string text) { if (scoreboardText != null) scoreboardText.text = text; }
        public void SetSecretColor(Color color) { if (secretSwatch != null) secretSwatch.color = color; }
        public void SetNextLabel(string text) { if (nextLabel != null) nextLabel.text = text; }

        public void ShowSecret(bool visible) { if (secretSwatch != null) secretSwatch.gameObject.SetActive(visible); }
        public void ShowClueControls(bool visible)
        {
            if (clueInput != null) clueInput.gameObject.SetActive(visible);
            if (submitButton != null) submitButton.gameObject.SetActive(visible);
        }
        public void ShowNext(bool visible) { if (nextButton != null) nextButton.gameObject.SetActive(visible); }

        /// <summary>Shows/hides the whole HUD (hidden while the menu/lobby is up).</summary>
        public void SetVisible(bool visible) { gameObject.SetActive(visible); }
    }
}
