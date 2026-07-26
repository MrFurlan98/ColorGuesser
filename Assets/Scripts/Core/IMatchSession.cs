using System;

namespace HuesNCues.Core
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

        void Start();
        void Send(IMatchCommand command);

        /// <summary>
        /// Starts a fresh match with the same participants (scores reset). Offline this
        /// rebuilds the local match; online the host rebuilds it for everyone.
        /// </summary>
        void RequestRestart();
    }
}
