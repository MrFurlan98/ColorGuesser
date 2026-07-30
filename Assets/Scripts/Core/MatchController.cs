using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorGuesser.Core
{
    /// <summary>
    /// The "Match State Machine" from the proposal's architecture. It runs a local
    /// match end to end: rounds, a rotating cue master, a secret target color, two
    /// clue words, up to two guesses per player, then reveal and scoring.
    ///
    /// It is pure logic - no Unity, no network - so it is fully unit testable and can
    /// later be driven by the authoritative host without changing the rules. The UI
    /// calls the Submit* methods and reads the public state to draw itself.
    ///
    /// Flow per round:
    ///   CueMasterClue1 -> Guessing1 -> CueMasterClue2 -> Guessing2 -> Reveal
    /// then NextRound() starts the next round or ends the match.
    /// </summary>
    public class MatchController : IReadOnlyMatch
    {
        private readonly List<Player> _players;
        private readonly ColorBoard _board;
        private readonly Random _rng;

        // A player's locked-in guesses for the current round, keyed by player id.
        private readonly Dictionary<string, GridCoordinate> _guess1 = new Dictionary<string, GridCoordinate>();
        private readonly Dictionary<string, GridCoordinate> _guess2 = new Dictionary<string, GridCoordinate>();

        // Points each player earned in the round just revealed (not the running total).
        private readonly Dictionary<string, int> _roundScores = new Dictionary<string, int>();

        // One entry per finished round, for the end-of-match stats.
        private readonly List<RoundRecord> _history = new List<RoundRecord>();
        private DateTime _startedAtUtc;
        private DateTime? _finishedAtUtc;

        // Whose turn it is to give clues, fixed for the duration of the round.
        private string _cueMasterId;

        public IReadOnlyList<Player> Players => _players;

        /// <summary>Points needed to win; rounds continue until a player reaches it.</summary>
        public int TargetScore { get; }
        public int RoundNumber { get; private set; }               // 1-based; 0 before start
        public MatchPhase Phase { get; private set; } = MatchPhase.NotStarted;
        public GridCoordinate Target { get; private set; }
        public string Clue1 { get; private set; }
        public string Clue2 { get; private set; }

        public IReadOnlyDictionary<string, GridCoordinate> FirstGuesses => _guess1;
        public IReadOnlyDictionary<string, GridCoordinate> SecondGuesses => _guess2;
        public IReadOnlyDictionary<string, int> RoundScores => _roundScores;
        public IReadOnlyList<RoundRecord> History => _history;

        /// <summary>How long the match has been running (frozen once it finishes).</summary>
        public float ElapsedSeconds =>
            (float)((_finishedAtUtc ?? DateTime.UtcNow) - _startedAtUtc).TotalSeconds;

        /// <summary>Raised after any state change, so a view can refresh itself.</summary>
        public event Action StateChanged;

        public MatchController(IEnumerable<Player> players, ColorBoard board, int targetScore, Random rng = null)
        {
            _players = players?.ToList() ?? throw new ArgumentNullException(nameof(players));
            if (_players.Count < 2) throw new ArgumentException("A match needs at least 2 players.");
            _board = board ?? throw new ArgumentNullException(nameof(board));
            if (targetScore < 1) throw new ArgumentException("The target score must be at least 1.");
            TargetScore = targetScore;
            _rng = rng ?? new Random();
        }

        /// <summary>True once any player has reached the target score.</summary>
        public bool HasWinner => _players.Any(p => p.Score >= TargetScore);

        /// <summary>
        /// The player giving clues this round. Decided once, when the round begins, and
        /// then fixed for the whole round.
        ///
        /// It has to be fixed rather than computed on demand: who the cue master is decides
        /// who counts as a guesser, and therefore whose cubes score. If it could change
        /// mid-round - which is exactly what happens when someone drops out or comes back -
        /// a player could give both clues and then be scored as a guesser, or earn cubes
        /// and have them credited to nobody.
        /// </summary>
        public Player CueMaster =>
            _cueMasterId == null ? null : _players.FirstOrDefault(p => p.Id == _cueMasterId);

        /// <summary>
        /// Picks the cue master for the round about to start: the rotation by round number,
        /// stepping past anyone who has dropped out so an absent player cannot be handed
        /// the turn.
        /// </summary>
        private void ChooseCueMaster()
        {
            int start = (RoundNumber - 1) % _players.Count;
            for (int step = 0; step < _players.Count; step++)
            {
                var candidate = _players[(start + step) % _players.Count];
                if (candidate.IsConnected) { _cueMasterId = candidate.Id; return; }
            }
            _cueMasterId = _players[start].Id; // everyone has dropped; keep a stable answer
        }

        /// <summary>Everyone except the cue master who is still connected, i.e. the
        /// players the round actually waits for.</summary>
        public IEnumerable<Player> Guessers
        {
            get
            {
                var cue = CueMaster;   // resolved once, not per player
                return _players.Where(p => p != cue && p.IsConnected);
            }
        }

        public void StartMatch()
        {
            RoundNumber = 0;
            _history.Clear();
            _startedAtUtc = DateTime.UtcNow;
            _finishedAtUtc = null;
            BeginRound();
        }

        private void BeginRound()
        {
            RoundNumber++;
            ChooseCueMaster();    // fixed for this round; presence is read once, here
            _guess1.Clear();
            _guess2.Clear();
            _roundScores.Clear(); // last round's points must not leak into this one
            Clue1 = Clue2 = null;
            Target = new GridCoordinate(_rng.Next(ColorBoard.Columns), _rng.Next(ColorBoard.Rows));
            Phase = MatchPhase.CueMasterClue1;
            StateChanged?.Invoke();
        }

        /// <summary>
        /// The cue master submits a clue word. Valid only in the two clue phases and
        /// only from the current cue master. Returns false if the action is not allowed.
        /// </summary>
        public bool SubmitClue(string playerId, string word)
        {
            if (Phase != MatchPhase.CueMasterClue1 && Phase != MatchPhase.CueMasterClue2) return false;
            if (CueMaster == null || playerId != CueMaster.Id) return false;
            if (string.IsNullOrWhiteSpace(word)) return false;

            if (Phase == MatchPhase.CueMasterClue1) { Clue1 = word.Trim(); Phase = MatchPhase.Guessing1; }
            else { Clue2 = word.Trim(); Phase = MatchPhase.Guessing2; }

            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// A guesser locks a cell. Valid only in the guessing phases, only from a
        /// non-cue-master player who has not guessed yet this phase (guesses lock on
        /// confirm). When every guesser has locked in, the round advances on its own.
        /// </summary>
        public bool SubmitGuess(string playerId, GridCoordinate coord)
        {
            var dict = Phase == MatchPhase.Guessing1 ? _guess1
                     : Phase == MatchPhase.Guessing2 ? _guess2
                     : null;
            if (dict == null) return false;
            if (!_board.Contains(coord)) return false;

            var player = _players.FirstOrDefault(p => p.Id == playerId);
            if (player == null || player == CueMaster) return false;
            if (dict.ContainsKey(playerId)) return false; // already locked this phase

            dict[playerId] = coord;

            if (dict.Count >= Guessers.Count()) // every connected guesser is in
            {
                if (Phase == MatchPhase.Guessing1) Phase = MatchPhase.CueMasterClue2;
                else RevealAndScore();
            }

            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Ends the current timed phase early (the round timer ran out) and moves to the
        /// next one. A cue master who did not type a clue simply leaves it empty; a
        /// player who did not lock a guess scores nothing for that cube. Valid during
        /// the clue and guessing phases; returns false otherwise.
        /// </summary>
        public bool ForceAdvancePhase()
        {
            switch (Phase)
            {
                case MatchPhase.CueMasterClue1:
                    Phase = MatchPhase.Guessing1;
                    break;
                case MatchPhase.Guessing1:
                    Phase = MatchPhase.CueMasterClue2;
                    break;
                case MatchPhase.CueMasterClue2:
                    Phase = MatchPhase.Guessing2;
                    break;
                case MatchPhase.Guessing2:
                    RevealAndScore();
                    break;
                default:
                    return false;
            }

            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Marks a player as having dropped out. They keep their score and stay on the
        /// scoreboard, but the round stops waiting for them: if it was their turn to give
        /// a clue, or they were the last guess the phase needed, play moves on.
        ///
        /// They keep the cue master's seat for the rest of the round if it was theirs -
        /// the clue they already gave, and the cubes it earned, still belong to them - and
        /// the rotation passes them over from the next round on.
        ///
        /// Returns false if the id is unknown or they had already dropped.
        /// </summary>
        public bool DropPlayer(string playerId)
        {
            var player = _players.FirstOrDefault(p => p.Id == playerId);
            if (player == null || !player.IsConnected) return false;

            bool wasCueMaster = CueMaster == player;
            player.IsConnected = false;

            switch (Phase)
            {
                // Their clue is never coming, so stop waiting for it.
                case MatchPhase.CueMasterClue1:
                case MatchPhase.CueMasterClue2:
                    if (wasCueMaster) ForceAdvancePhase();
                    break;

                // The round may now have every guess it is still waiting for.
                case MatchPhase.Guessing1:
                    if (_guess1.Count >= Guessers.Count()) ForceAdvancePhase();
                    break;
                case MatchPhase.Guessing2:
                    if (_guess2.Count >= Guessers.Count()) ForceAdvancePhase();
                    break;
            }

            StateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Puts a player who had dropped out back in their seat: same id, same score, same
        /// place in the cue master rotation. The round starts waiting for their guess again
        /// if it is still open and they had not already locked one in.
        ///
        /// Deliberately does not touch the cue master, so returning mid-round cannot take
        /// the turn back off whoever is holding it.
        ///
        /// Returns false if the id is unknown or they were never away.
        /// </summary>
        public bool RejoinPlayer(string playerId)
        {
            var player = _players.FirstOrDefault(p => p.Id == playerId);
            if (player == null || player.IsConnected) return false;

            player.IsConnected = true;
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>How many players are still in the match.</summary>
        public int ConnectedCount => _players.Count(p => p.IsConnected);

        /// <summary>True while the round is in a phase the timer applies to.</summary>
        public static bool IsTimedPhase(MatchPhase phase) =>
            phase == MatchPhase.CueMasterClue1 || phase == MatchPhase.Guessing1 ||
            phase == MatchPhase.CueMasterClue2 || phase == MatchPhase.Guessing2;

        private void RevealAndScore()
        {
            _roundScores.Clear();

            // Each guesser scores both of their cubes by proximity to the target.
            foreach (var guesser in Guessers)
            {
                int points = 0;
                if (_guess1.TryGetValue(guesser.Id, out var g1)) points += ScoringService.PointsForGuess(Target, g1);
                if (_guess2.TryGetValue(guesser.Id, out var g2)) points += ScoringService.PointsForGuess(Target, g2);
                guesser.Score += points;
                _roundScores[guesser.Id] = points;
            }

            // The cue master scores for every cube that landed in the scoring rings. They
            // are credited even if they dropped out mid-round: the clue was theirs.
            var cueMaster = CueMaster;
            if (cueMaster != null)
            {
                var allCubes = _guess1.Values.Concat(_guess2.Values);
                int cuePoints = ScoringService.PointsForCueGiver(Target, allCubes, _players.Count);
                cueMaster.Score += cuePoints;
                _roundScores[cueMaster.Id] = cuePoints;
            }

            // Keep the round for the end-of-match stats.
            int exact = 0;
            foreach (var cube in _guess1.Values.Concat(_guess2.Values))
                if (Target.DistanceTo(cube) == 0) exact++;

            int total = 0;
            foreach (var points in _roundScores.Values) total += points;

            _history.Add(new RoundRecord
            {
                RoundNumber = RoundNumber,
                Clue1 = Clue1,
                Clue2 = Clue2,
                Target = Target,
                TotalPoints = total,
                ExactGuesses = exact,
            });

            Phase = MatchPhase.Reveal;
        }

        /// <summary>
        /// Advances from the reveal to the next round, or ends the match once a player
        /// has reached the target score. Valid only during Reveal; false otherwise.
        /// </summary>
        public bool NextRound()
        {
            if (Phase != MatchPhase.Reveal) return false;

            if (HasWinner)
            {
                Phase = MatchPhase.Finished;
                _finishedAtUtc = DateTime.UtcNow; // freezes ElapsedSeconds
                StateChanged?.Invoke();
                return true;
            }

            BeginRound();
            return true;
        }
    }
}
