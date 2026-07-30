using System;

namespace ColorGuesser.Core
{
    /// <summary>
    /// The seam between the game UI and "how a match is run" (Facade / Gateway - the
    /// proposal's Session Controller). The UI reads state from Match, listens to
    /// StateChanged, and sends commands with Send - never touching MatchController or
    /// any networking API directly.
    ///
    /// Implementations:
    ///   - LocalMatchSession: applies commands in-process (single machine).
    ///   - (later) a network session: sends commands to the authoritative host and
    ///     raises StateChanged when the host broadcasts the new state.
    /// The UI code is identical for both.
    /// </summary>
    public interface IMatchSession
    {
        IReadOnlyMatch Match { get; }
        event Action StateChanged;

        /// <summary>
        /// The id of the player this client controls, or null for hotseat/offline
        /// (where one screen drives every player in turn). The UI uses this to allow
        /// only your own actions and to hide the secret color from non-cue-masters.
        /// </summary>
        string LocalPlayerId { get; }

        /// <summary>True if this client controls match flow (offline, or the network host).
        /// Used to gate host-only actions like Next Round / Play Again.</summary>
        bool IsHost { get; }

        /// <summary>Seconds left in the current timed phase (clue or guessing), or 0 when
        /// there is no timer (offline games, or the host chose "no limit").</summary>
        float PhaseSecondsLeft { get; }

        /// <summary>How long the timed phase lasts in total, for progress bars. 0 = untimed.</summary>
        float PhaseSecondsTotal { get; }

        /// <summary>
        /// Says this player is ready to leave the reveal. The round advances once every
        /// player has said so (or the reveal timer runs out).
        /// </summary>
        void VoteNextRound();

        /// <summary>How many players are ready to move on, and how many are needed.</summary>
        int NextRoundVotes { get; }
        int NextRoundVotesNeeded { get; }

        /// <summary>Seconds before the reveal auto-advances (0 when not counting down).</summary>
        float NextRoundSecondsLeft { get; }

        void Start();
        void Send(IMatchCommand command);

        /// <summary>
        /// Play again. Offline this rebuilds the local match straight away; online the
        /// host sends everyone back to the lobby (scores cleared, players not ready) so
        /// they can ready up and change colours before the next match.
        /// </summary>
        void RequestRestart();
    }
}
