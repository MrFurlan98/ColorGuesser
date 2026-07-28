using System;
using System.Collections.Generic;
using System.Linq;

namespace HuesNCues.Core
{
    /// <summary>
    /// A single-machine session: commands are applied straight to an in-process
    /// MatchController. This is the "host" and the "client" in one, which is why it
    /// can validate and apply immediately. The network session (MatchNetwork) keeps
    /// the same public surface but routes commands through the transport.
    /// </summary>
    public sealed class LocalMatchSession : IMatchSession
    {
        private readonly List<(string Id, string Name, int ColorIndex)> _seeds;
        private readonly ColorBoard _board;
        private readonly int _targetScore;
        private readonly Random _rng;

        private MatchController _match;

        public IReadOnlyMatch Match => _match;
        public event Action StateChanged;

        // Offline is hotseat: one screen controls every player, so there is no
        // single "local" player, and it always controls match flow.
        public string LocalPlayerId => null;
        public bool IsHost => true;

        public LocalMatchSession(IEnumerable<Player> players, ColorBoard board, int targetScore, Random rng = null)
        {
            _seeds = players.Select(p => (p.Id, p.Name, p.ColorIndex)).ToList();
            _board = board;
            _targetScore = targetScore;
            _rng = rng;
            _match = Build();
        }

        private MatchController Build()
        {
            // Fresh Player objects so a restart resets scores.
            var m = new MatchController(
                _seeds.Select(s => new Player(s.Id, s.Name, s.ColorIndex)), _board, _targetScore, _rng);
            m.StateChanged += RaiseStateChanged;
            return m;
        }

        public void Start() => _match.StartMatch();

        public void Send(IMatchCommand command) => command?.ApplyTo(_match);

        public void RequestRestart()
        {
            _match.StateChanged -= RaiseStateChanged;
            _match = Build();
            _match.StartMatch();
        }

        private void RaiseStateChanged() => StateChanged?.Invoke();
    }
}
