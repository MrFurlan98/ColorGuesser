using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>One row of lobby data handed to the view (no networking types here).</summary>
    public struct LobbyPlayerInfo
    {
        public string Name;
        public int ColorIndex;
        public bool IsHost;
        public bool IsReady;
    }

    /// <summary>
    /// Passive view for the in-room lobby: room code (+ copy), the player grid, and the
    /// main action button - "Start" for the host, "Ready" for everyone else.
    /// Logic lives in LobbyController. Lives on the LobbyHud prefab.
    /// </summary>
    public class LobbyHud : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI codeText;
        [SerializeField] private Button copyCodeButton;

        [Tooltip("Shows how many players are in the room.")]
        [SerializeField] private TextMeshProUGUI numberOfPlayersText;

        [Header("Player grid")]
        [Tooltip("The Grid Layout Group's transform: cards are spawned as its children.")]
        [SerializeField] private Transform playerGrid;
        [SerializeField] private PlayerLobbyCard cardPrefab;

        [Header("Room settings (host only)")]
        [SerializeField] private StepperControl maxPlayersStepper;
        [SerializeField] private StepperControl targetScoreStepper;
        [SerializeField] private ChipGroupControl guessTimeChips;

        [Header("Buttons")]
        [Tooltip("Starts the match (host) or toggles ready (everyone else).")]
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionLabel;
        [SerializeField] private Button leaveButton;

        private readonly List<PlayerLobbyCard> _cards = new List<PlayerLobbyCard>();

        // The values each dropdown option maps to (index -> value).
        public static readonly int[] MaxPlayerOptions = { 3, 4, 5, 6, 7, 8, 9, 10 };
        public static readonly int[] TargetScoreOptions = { 10, 15, 20, 25, 30, 40, 50 };
        public static readonly int[] GuessTimeOptions = { 0, 15, 30, 45, 60, 90, 120 };

        public event Action ActionClicked;   // Start (host) / Ready (client)
        public event Action LeaveClicked;
        public event Action CopyCodeClicked;

        /// <summary>Raised with (maxPlayers, targetScore, guessSeconds) when the host edits a dropdown.</summary>
        public event Action<int, int, int> SettingsChanged;

        private void Awake()
        {
            if (actionButton != null) actionButton.onClick.AddListener(() => ActionClicked?.Invoke());
            if (leaveButton != null) leaveButton.onClick.AddListener(() => LeaveClicked?.Invoke());
            if (copyCodeButton != null) copyCodeButton.onClick.AddListener(() => CopyCodeClicked?.Invoke());

            // Options and labels come from code, so widgets and values can never drift.
            if (maxPlayersStepper != null)
            {
                maxPlayersStepper.Configure(MaxPlayerOptions, "{0} jogadores");
                maxPlayersStepper.ValueChanged += _ => RaiseSettingsChanged();
            }
            if (targetScoreStepper != null)
            {
                targetScoreStepper.Configure(TargetScoreOptions, "{0} pontos");
                targetScoreStepper.ValueChanged += _ => RaiseSettingsChanged();
            }
            if (guessTimeChips != null)
            {
                guessTimeChips.Configure(GuessTimeOptions, "{0}s", "Sem limite");
                guessTimeChips.ValueChanged += _ => RaiseSettingsChanged();
            }
        }

        private void RaiseSettingsChanged() =>
            SettingsChanged?.Invoke(SelectedMaxPlayers, SelectedTargetScore, SelectedGuessSeconds);

        public int SelectedMaxPlayers => maxPlayersStepper != null ? maxPlayersStepper.Value : 6;
        public int SelectedTargetScore => targetScoreStepper != null ? targetScoreStepper.Value : 25;
        public int SelectedGuessSeconds => guessTimeChips != null ? guessTimeChips.Value : 60;

        /// <summary>Shows the synced settings (used by clients, who cannot edit them).</summary>
        public void SetSettings(int maxPlayers, int targetScore, int guessSeconds)
        {
            if (maxPlayersStepper != null) maxPlayersStepper.SetValue(maxPlayers);
            if (targetScoreStepper != null) targetScoreStepper.SetValue(targetScore);
            if (guessTimeChips != null) guessTimeChips.SetValue(guessSeconds);
        }

        /// <summary>Only the host may edit the room settings.</summary>
        public void SetSettingsInteractable(bool value)
        {
            if (maxPlayersStepper != null) maxPlayersStepper.SetInteractable(value);
            if (targetScoreStepper != null) targetScoreStepper.SetInteractable(value);
            if (guessTimeChips != null) guessTimeChips.SetInteractable(value);
        }

        public void SetCode(string text) { if (codeText != null) codeText.text = text; }

        /// <summary>Updates the "players in the room" counter (e.g. "2/4").</summary>
        public void SetPlayerCount(int count, int max)
        {
            if (numberOfPlayersText != null) numberOfPlayersText.text = $"{count} de {max} Jogadores";
        }
        public void SetActionLabel(string text) { if (actionLabel != null) actionLabel.text = text; }
        public void SetActionInteractable(bool value) { if (actionButton != null) actionButton.interactable = value; }
        public void SetActionVisible(bool visible) { if (actionButton != null) actionButton.gameObject.SetActive(visible); }
        public void SetCopyVisible(bool visible) { if (copyCodeButton != null) copyCodeButton.gameObject.SetActive(visible); }
        public void SetVisible(bool visible) { gameObject.SetActive(visible); }

        /// <summary>
        /// Rebuilds the player grid. Cards are reused (only created/hidden as the
        /// player count changes) so the layout does not churn every lobby update.
        /// </summary>
        public void SetPlayers(IList<LobbyPlayerInfo> players)
        {
            if (playerGrid == null || cardPrefab == null) return;

            while (_cards.Count < players.Count)
                _cards.Add(Instantiate(cardPrefab, playerGrid));

            for (int i = 0; i < _cards.Count; i++)
            {
                bool used = i < players.Count;
                _cards[i].gameObject.SetActive(used);
                if (!used) continue;

                var p = players[i];
                _cards[i].Set(p.Name, p.ColorIndex, p.IsHost, p.IsReady);
            }
        }
    }
}
