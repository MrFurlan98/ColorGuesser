using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColorGuesser.Core;
using UnityEngine;

namespace ColorGuesser.Net
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
        public int targetScore;
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
        public int[] playerColors;
        public int[] playerRoundScores; // points earned in the round just revealed
        public bool[] playerConnected;  // false for players who dropped out

        public string[] g1Ids;
        public int[] g1Cols;
        public int[] g1Rows;
        public string[] g2Ids;
        public int[] g2Cols;
        public int[] g2Rows;

        // Finished-round history, for the end-of-match stats.
        public int[] hRound;
        public string[] hClue1;
        public string[] hClue2;
        public int[] hTargetCol;
        public int[] hTargetRow;
        public int[] hPoints;
        public int[] hExact;
        public float elapsedSeconds;

        public static MatchSnapshot Capture(IReadOnlyMatch m)
        {
            var players = m.Players;
            var snap = new MatchSnapshot
            {
                targetScore = m.TargetScore,
                roundNumber = m.RoundNumber,
                phase = (int)m.Phase,
                targetCol = m.Target.Column,
                targetRow = m.Target.Row,
                clue1 = m.Clue1 ?? "",
                clue2 = m.Clue2 ?? "",
                playerIds = players.Select(p => p.Id).ToArray(),
                playerNames = players.Select(p => p.Name).ToArray(),
                playerScores = players.Select(p => p.Score).ToArray(),
                playerColors = players.Select(p => p.ColorIndex).ToArray(),
                playerRoundScores = players
                    .Select(p => m.RoundScores.TryGetValue(p.Id, out var s) ? s : 0).ToArray(),
                playerConnected = players.Select(p => p.IsConnected).ToArray(),
                cueMasterIndex = m.CueMaster == null ? -1 : IndexOf(players, m.CueMaster.Id),
            };
            CaptureGuesses(m.FirstGuesses, out snap.g1Ids, out snap.g1Cols, out snap.g1Rows);
            CaptureGuesses(m.SecondGuesses, out snap.g2Ids, out snap.g2Cols, out snap.g2Rows);

            var history = m.History;
            snap.elapsedSeconds = m.ElapsedSeconds;
            snap.hRound = history.Select(r => r.RoundNumber).ToArray();
            snap.hClue1 = history.Select(r => r.Clue1 ?? "").ToArray();
            snap.hClue2 = history.Select(r => r.Clue2 ?? "").ToArray();
            snap.hTargetCol = history.Select(r => r.Target.Column).ToArray();
            snap.hTargetRow = history.Select(r => r.Target.Row).ToArray();
            snap.hPoints = history.Select(r => r.TotalPoints).ToArray();
            snap.hExact = history.Select(r => r.ExactGuesses).ToArray();
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
        private readonly Dictionary<string, int> _roundScores = new Dictionary<string, int>();
        private readonly List<RoundRecord> _history = new List<RoundRecord>();
        private int _cueIndex = -1;

        public int TargetScore { get; private set; }
        public int RoundNumber { get; private set; }
        public MatchPhase Phase { get; private set; } = MatchPhase.NotStarted;
        public GridCoordinate Target { get; private set; }
        public string Clue1 { get; private set; }
        public string Clue2 { get; private set; }

        public IReadOnlyList<Player> Players => _players;
        public Player CueMaster => (_cueIndex >= 0 && _cueIndex < _players.Count) ? _players[_cueIndex] : null;
        // Filtered the same way as MatchController.Guessers, so the guess panel and its
        // "x/y confirmed" counter agree with what the host is waiting for.
        public IEnumerable<Player> Guessers =>
            _players.Where(p => p != CueMaster && p.IsConnected);
        public IReadOnlyDictionary<string, GridCoordinate> FirstGuesses => _g1;
        public IReadOnlyDictionary<string, GridCoordinate> SecondGuesses => _g2;
        public IReadOnlyDictionary<string, int> RoundScores => _roundScores;
        public IReadOnlyList<RoundRecord> History => _history;
        public float ElapsedSeconds { get; private set; }

        public void Apply(MatchSnapshot s)
        {
            TargetScore = s.targetScore;
            RoundNumber = s.roundNumber;
            Phase = (MatchPhase)s.phase;
            Target = new GridCoordinate(s.targetCol, s.targetRow);
            Clue1 = s.clue1;
            Clue2 = s.clue2;
            _cueIndex = s.cueMasterIndex;

            _players.Clear();
            _roundScores.Clear();
            int count = s.playerIds != null ? s.playerIds.Length : 0;
            for (int i = 0; i < count; i++)
            {
                int colorIndex = (s.playerColors != null && i < s.playerColors.Length) ? s.playerColors[i] : 0;
                bool connected = s.playerConnected == null || i >= s.playerConnected.Length || s.playerConnected[i];
                _players.Add(new Player(s.playerIds[i], s.playerNames[i], colorIndex)
                {
                    Score = s.playerScores[i],
                    IsConnected = connected,
                });
                if (s.playerRoundScores != null && i < s.playerRoundScores.Length)
                    _roundScores[s.playerIds[i]] = s.playerRoundScores[i];
            }

            Fill(_g1, s.g1Ids, s.g1Cols, s.g1Rows);
            Fill(_g2, s.g2Ids, s.g2Cols, s.g2Rows);

            ElapsedSeconds = s.elapsedSeconds;
            _history.Clear();
            int rounds = s.hRound != null ? s.hRound.Length : 0;
            for (int i = 0; i < rounds; i++)
                _history.Add(new RoundRecord
                {
                    RoundNumber = s.hRound[i],
                    Clue1 = s.hClue1[i],
                    Clue2 = s.hClue2[i],
                    Target = new GridCoordinate(s.hTargetCol[i], s.hTargetRow[i]),
                    TotalPoints = s.hPoints[i],
                    ExactGuesses = s.hExact[i],
                });
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
