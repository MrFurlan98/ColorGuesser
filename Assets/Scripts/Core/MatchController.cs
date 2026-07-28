using System;
using System.Collections.Generic;
using System.Linq;

namespace HuesNCues.Core
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

        /// <summary>The player giving clues this round (rotates each round).</summary>
        public Player CueMaster =>
            Phase == MatchPhase.NotStarted ? null : _players[(RoundNumber - 1) % _players.Count];

        /// <summary>Everyone except the cue master, i.e. the players who guess.</summary>
        public IEnumerable<Player> Guessers => _players.Where(p => p != CueMaster);

        public void StartMatch()
        {
            RoundNumber = 0;
            BeginRound();
        }

        private void BeginRound()
        {
            RoundNumber++;
            _guess1.Clear();
            _guess2.Clear();
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

            if (dict.Count >= _players.Count - 1) // all guessers are in
            {
                if (Phase == MatchPhase.Guessing1) Phase = MatchPhase.CueMasterClue2;
                else RevealAndScore();
            }

            StateChanged?.Invoke();
            return true;
        }

        private void RevealAndScore()
        {
            // Each guesser scores both of their cubes by proximity to the target.
            foreach (var guesser in Guessers)
            {
                int points = 0;
                if (_guess1.TryGetValue(guesser.Id, out var g1)) points += ScoringService.PointsForGuess(Target, g1);
                if (_guess2.TryGetValue(guesser.Id, out var g2)) points += ScoringService.PointsForGuess(Target, g2);
                guesser.Score += points;
            }

            // The cue master scores for every cube that landed in the scoring rings.
            var allCubes = _guess1.Values.Concat(_guess2.Values);
            CueMaster.Score += ScoringService.PointsForCueGiver(Target, allCubes);

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
                StateChanged?.Invoke();
                return true;
            }

            BeginRound();
            return true;
        }
    }
}
