using System;
using ColorGuesser.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorGuesser.Game
{
    /// <summary>
    /// Passive view (Humble Object / MVP pattern) for the match HUD, and the facade the
    /// rest of the game talks to. It owns the clue controls, and forwards everything
    /// else to sub-views that sit on the matching prefab objects:
    /// RoundTitlesView (Titles), PhaseStepsView (Phases), ClueView + RoundTimerView +
    /// RoleTitlesView (GameInfo), RolePanelsView, GuessPanelView, ColorDisplayView,
    /// ScorePanelView, FinalScorePanelView and StatsPanelView.
    ///
    /// It contains NO game rules and does not know about MatchController - MatchView
    /// drives it. Lives on the MatchHud prefab so layout and sprites are authored in
    /// the editor.
    /// </summary>
    public class MatchHud : MonoBehaviour
    {
        [Header("Sub-views (each sits on its own object in the prefab)")]
        [Tooltip("The 'Titles' object: round number, title, subtitle.")]
        [SerializeField] private RoundTitlesView titles;

        [Tooltip("The 'Phases' object: the 5-step progress strip.")]
        [SerializeField] private PhaseStepsView phaseSteps;

        [Tooltip("The 'Clue' object inside GameInfo: the clue words.")]
        [SerializeField] private ClueView clue;

        [Tooltip("The 'Timer' object inside GameInfo: countdown + slider.")]
        [SerializeField] private RoundTimerView roundTimer;

        [Tooltip("The 'Clues/Guesses' object: swaps the clue panel and the guess panel.")]
        [SerializeField] private RolePanelsView rolePanels;

        [Tooltip("The 'Guess' panel: confirm button, guesser toggles and counter.")]
        [SerializeField] private GuessPanelView guessPanel;

        [Tooltip("The 'Titles' inside GameInfo: wording changes for the cue master.")]
        [SerializeField] private RoleTitlesView roleTitles;

        [Tooltip("The score panel shown at the reveal.")]
        [SerializeField] private ScorePanelView scorePanel;

        [Tooltip("The final scoreboard shown once the match is over.")]
        [SerializeField] private FinalScorePanelView finalScorePanel;

        [Tooltip("The end-of-match stats panel.")]
        [SerializeField] private StatsPanelView statsPanel;

        [Tooltip("Shared colour + code display: the secret for the cue master, the picked " +
                 "cell for a guesser, and the target at the reveal.")]
        [SerializeField] private ColorDisplayView colorDisplay;

        [Header("Controls")]
        [SerializeField] private TMP_InputField clueInput;
        [SerializeField] private Button submitButton;

        [Tooltip("Area the colour board is placed into. Leave empty to let BoardView " +
                 "position the board on the canvas itself.")]
        [SerializeField] private RectTransform boardContainer;

        [Tooltip("Everything used while a round is being played (Board + Inputs + Score). " +
                 "Hidden once the match ends so the final score screen stands alone.")]
        [SerializeField] private GameObject gameplayRoot;

        [Tooltip("The 'Final Score' container holding the scoreboard and the stats panel. " +
                 "Shown only when the match is over.")]
        [SerializeField] private GameObject finalScreenRoot;

        [Tooltip("The 'GameInfo' container (timer, clue, role panels). Shown while a round " +
                 "is being played and hidden at the reveal, where the score panel takes over.")]
        [SerializeField] private GameObject gameInfoRoot;

        /// <summary>Raised when the cue master submits a clue (button or Enter key).</summary>
        public event Action SubmitClueRequested;


        /// <summary>Raised when the player confirms the cell they picked.</summary>
        public event Action ConfirmGuessRequested;

        /// <summary>Raised when the player presses next/finish on the score panel.</summary>
        public event Action NextRoundVoteRequested;

        /// <summary>Raised when the host restarts from the stats panel.</summary>
        public event Action PlayAgainRequested;

        /// <summary>Raised when the player leaves for the main menu.</summary>
        public event Action MenuRequested;

        private void Awake()
        {
            if (submitButton != null) submitButton.onClick.AddListener(() => SubmitClueRequested?.Invoke());
            if (clueInput != null) clueInput.onSubmit.AddListener(_ => SubmitClueRequested?.Invoke());
            if (guessPanel != null) guessPanel.ConfirmClicked += () => ConfirmGuessRequested?.Invoke();
            if (scorePanel != null) scorePanel.NextClicked += () => NextRoundVoteRequested?.Invoke();
            if (statsPanel != null)
            {
                statsPanel.PlayAgainClicked += () => PlayAgainRequested?.Invoke();
                statsPanel.MenuClicked += () => MenuRequested?.Invoke();
            }
        }

        // ----- Controls -------------------------------------------------------------

        public string ClueText
        {
            get => clueInput != null ? clueInput.text : string.Empty;
            set { if (clueInput != null) clueInput.text = value; }
        }


        /// <summary>
        /// Drives the shared colour display: the secret colour for the cue master, the
        /// picked cell for a guesser, or the target at the reveal. Pass false to clear.
        /// </summary>
        public void ShowColor(bool hasColor, Color color, string code, string colorName = null)
        {
            if (colorDisplay == null) return;
            if (hasColor) colorDisplay.Show(true, color, code, colorName);
            else colorDisplay.Clear();
        }

        /// <summary>
        /// Enables/disables the clue field and its confirm button. They stay on screen
        /// after submitting - greyed out rather than disappearing - so the panel does
        /// not jump around between phases.
        /// </summary>
        public void SetClueControlsEnabled(bool enabled)
        {
            if (clueInput != null)
            {
                clueInput.interactable = enabled;
                if (!enabled) clueInput.DeactivateInputField(); // drop focus/caret
            }
            if (submitButton != null) submitButton.interactable = enabled;
        }

        /// <summary>The area the board should be placed into (null = BoardView decides).</summary>
        public RectTransform BoardContainer => boardContainer;

        /// <summary>Shows/hides the whole HUD (hidden while the menu/lobby is up).</summary>
        public void SetVisible(bool visible) { gameObject.SetActive(visible); }

        /// <summary>
        /// Shows/hides the round UI (phases, board, clue and guess panels). Turned off at
        /// the end of the match so only the final score and stats are on screen.
        /// </summary>
        public void ShowGameplay(bool visible)
        {
            if (gameplayRoot != null && gameplayRoot.activeSelf != visible)
                gameplayRoot.SetActive(visible);
        }

        /// <summary>
        /// Shows/hides the whole end-of-match screen (scoreboard + stats). Their own
        /// panels live inside it, so this parent has to be on for them to be visible.
        /// </summary>
        public void ShowFinalScreen(bool visible)
        {
            if (finalScreenRoot != null && finalScreenRoot.activeSelf != visible)
                finalScreenRoot.SetActive(visible);
        }

        /// <summary>
        /// Shows/hides the round info panel. It and the reveal score panel are siblings
        /// that must never be on together - one replaces the other.
        /// </summary>
        public void ShowGameInfo(bool visible)
        {
            if (gameInfoRoot != null && gameInfoRoot.activeSelf != visible)
                gameInfoRoot.SetActive(visible);
        }

        // ----- Forwarded to the sub-views -------------------------------------------

        public void SetRound(int round, bool matchOver = false)
        {
            if (titles != null) titles.SetRound(round, matchOver);
        }

        public void SetPhaseTexts(MatchPhase phase, bool isCueMaster)
        {
            if (titles != null) titles.SetPhaseTexts(phase, isCueMaster);
        }

        /// <summary>Shows the clue for the half of the round being played.</summary>
        public void SetClue(string currentClue, bool visible = true)
        {
            if (clue != null) clue.SetClue(currentClue, visible);
        }

        public void SetPhaseSteps(MatchPhase phase)
        {
            if (phaseSteps != null) phaseSteps.SetPhase(phase);
        }

        /// <summary>
        /// Shows the clue panel to the cue master and the guess panel to the rest, and
        /// switches the GameInfo titles to match that role.
        /// </summary>
        public void SetRolePanels(bool isCueMaster, bool visible)
        {
            if (rolePanels != null) rolePanels.SetRole(isCueMaster, visible);
            if (roleTitles != null) roleTitles.SetRole(isCueMaster);
        }

        public void SetGuessConfirmEnabled(bool enabled)
        {
            if (guessPanel != null) guessPanel.SetConfirmEnabled(enabled);
        }


        public void SetGuessPlayers(System.Collections.Generic.IList<GuessStatusInfo> players)
        {
            if (guessPanel != null) guessPanel.SetPlayers(players);
        }

        /// <summary>Shows the reveal score panel, or hides it outside the reveal.</summary>
        public void ShowScorePanel(System.Collections.Generic.IList<RoundScoreInfo> scores,
            bool matchDecided, bool alreadyVoted)
        {
            if (scorePanel == null) return;
            scorePanel.SetVisible(true);
            scorePanel.Show(scores, matchDecided, alreadyVoted);
        }

        public void HideScorePanel()
        {
            if (scorePanel != null) scorePanel.SetVisible(false);
        }

        /// <summary>Shows the end-of-match scoreboard (already ranked).</summary>
        public void ShowFinalScores(System.Collections.Generic.IList<FinalScoreInfo> scores)
        {
            if (finalScorePanel == null) return;
            finalScorePanel.SetVisible(true);
            finalScorePanel.Show(scores);
        }

        public void HideFinalScores()
        {
            if (finalScorePanel != null) finalScorePanel.SetVisible(false);
        }

        /// <summary>
        /// Shows the end-of-match stats panel. Everyone gets the Play Again button; only
        /// the host's press restarts, so a non-host who has pressed it sees it waiting.
        /// </summary>
        public void ShowStats(MatchStatsInfo stats, bool waitingForHost)
        {
            if (statsPanel == null) return;
            statsPanel.SetVisible(true);
            statsPanel.SetPlayAgainVisible(true);
            statsPanel.SetPlayAgainWaiting(waitingForHost);
            statsPanel.Show(stats);
        }

        public void HideStats()
        {
            if (statsPanel != null) statsPanel.SetVisible(false);
        }

        public void SetTimer(float secondsLeft, float secondsTotal)
        {
            if (roundTimer != null) roundTimer.SetTimer(secondsLeft, secondsTotal);
        }
    }
}
