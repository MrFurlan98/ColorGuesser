using System;
using System.Collections.Generic;

namespace HuesNCues.Core
{
    /// <summary>
    /// A single-machine session: commands are applied straight to an in-process
    /// MatchController. This is the "host" and the "client" in one, which is why it
    /// can validate and apply immediately. The network session (later) will keep the
    /// same public surface but route commands through the transport.
    /// </summary>
    public sealed class LocalMatchSession : IMatchSession
    {
        private readonly MatchController _match;

        public IReadOnlyMatch Match => _match;
        public event Action StateChanged;

        // Offline is hotseat: one screen controls every player, so there is no
        // single "local" player.
        public string LocalPlayerId => null;

        public LocalMatchSession(IEnumerable<Player> players, ColorBoard board, int totalRounds, Random rng = null)
        {
            _match = new MatchController(players, board, totalRounds, rng);
            _match.StateChanged += RaiseStateChanged;
        }

        public void Start() => _match.StartMatch();

        public void Send(IMatchCommand command) => command?.ApplyTo(_match);

        private void RaiseStateChanged() => StateChanged?.Invoke();
    }
}
