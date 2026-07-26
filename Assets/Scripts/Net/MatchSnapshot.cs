using System.Collections.Generic;
using System.Linq;
using System.Text;
using HuesNCues.Core;
using UnityEngine;

namespace HuesNCues.Net
{
    /// <summary>
    /// Serializable full snapshot of the match state. The host captures one from its
    /// authoritative MatchController after every change and broadcasts it as JSON
    /// bytes; clients apply it to a SnapshotMatch. Full snapshots keep the sync logic
    /// simple and robust (no per-field deltas), and the match state is small.
    /// </summary>
    [System.Serializable]
    public class MatchSnapshot
    {
        public int totalRounds;
        public int roundNumber;
        public int phase;
        public int targetCol;
        public int targetRow;
        public int cueMasterIndex;
        public string clue1;
        public string clue2;

        public string[] playerIds;
        public string[] playerNames;
        public int[] playerScores;

        public string[] g1Ids;
        public int[] g1Cols;
        public int[] g1Rows;
        public string[] g2Ids;
        public int[] g2Cols;
        public int[] g2Rows;

        public static MatchSnapshot Capture(IReadOnlyMatch m)
        {
            var players = m.Players;
            var snap = new MatchSnapshot
            {
                totalRounds = m.TotalRounds,
                roundNumber = m.RoundNumber,
                phase = (int)m.Phase,
                targetCol = m.Target.Column,
                targetRow = m.Target.Row,
                clue1 = m.Clue1 ?? "",
                clue2 = m.Clue2 ?? "",
                playerIds = players.Select(p => p.Id).ToArray(),
                playerNames = players.Select(p => p.Name).ToArray(),
                playerScores = players.Select(p => p.Score).ToArray(),
                cueMasterIndex = m.CueMaster == null ? -1 : IndexOf(players, m.CueMaster.Id),
            };
            CaptureGuesses(m.FirstGuesses, out snap.g1Ids, out snap.g1Cols, out snap.g1Rows);
            CaptureGuesses(m.SecondGuesses, out snap.g2Ids, out snap.g2Cols, out snap.g2Rows);
            return snap;
        }

        private static int IndexOf(IReadOnlyList<Player> players, string id)
        {
            for (int i = 0; i < players.Count; i++)
                if (players[i].Id == id) return i;
            return -1;
        }

        private static void CaptureGuesses(IReadOnlyDictionary<string, GridCoordinate> g,
            out string[] ids, out int[] cols, out int[] rows)
        {
            ids = g.Keys.ToArray();
            cols = ids.Select(id => g[id].Column).ToArray();
            rows = ids.Select(id => g[id].Row).ToArray();
        }

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
        public static MatchSnapshot FromBytes(byte[] bytes) => JsonUtility.FromJson<MatchSnapshot>(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>
    /// Client-side read model rebuilt from snapshots. Implements IReadOnlyMatch so the
    /// UI renders from it exactly as it would from the host's MatchController.
    /// </summary>
    public class SnapshotMatch : IReadOnlyMatch
    {
        private readonly List<Player> _players = new List<Player>();
        private readonly Dictionary<string, GridCoordinate> _g1 = new Dictionary<string, GridCoordinate>();
        private readonly Dictionary<string, GridCoordinate> _g2 = new Dictionary<string, GridCoordinate>();
        private int _cueIndex = -1;

        public int TotalRounds { get; private set; }
        public int RoundNumber { get; private set; }
        public MatchPhase Phase { get; private set; } = MatchPhase.NotStarted;
        public GridCoordinate Target { get; private set; }
        public string Clue1 { get; private set; }
        public string Clue2 { get; private set; }

        public IReadOnlyList<Player> Players => _players;
        public Player CueMaster => (_cueIndex >= 0 && _cueIndex < _players.Count) ? _players[_cueIndex] : null;
        public IEnumerable<Player> Guessers => _players.Where(p => p != CueMaster);
        public IReadOnlyDictionary<string, GridCoordinate> FirstGuesses => _g1;
        public IReadOnlyDictionary<string, GridCoordinate> SecondGuesses => _g2;

        public void Apply(MatchSnapshot s)
        {
            TotalRounds = s.totalRounds;
            RoundNumber = s.roundNumber;
            Phase = (MatchPhase)s.phase;
            Target = new GridCoordinate(s.targetCol, s.targetRow);
            Clue1 = s.clue1;
            Clue2 = s.clue2;
            _cueIndex = s.cueMasterIndex;

            _players.Clear();
            int count = s.playerIds != null ? s.playerIds.Length : 0;
            for (int i = 0; i < count; i++)
                _players.Add(new Player(s.playerIds[i], s.playerNames[i]) { Score = s.playerScores[i] });

            Fill(_g1, s.g1Ids, s.g1Cols, s.g1Rows);
            Fill(_g2, s.g2Ids, s.g2Cols, s.g2Rows);
        }

        private static void Fill(Dictionary<string, GridCoordinate> dict, string[] ids, int[] cols, int[] rows)
        {
            dict.Clear();
            if (ids == null) return;
            for (int i = 0; i < ids.Length; i++)
                dict[ids[i]] = new GridCoordinate(cols[i], rows[i]);
        }
    }
}
