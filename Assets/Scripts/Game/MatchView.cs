using System.Collections.Generic;
using System.Linq;
using HuesNCues.Core;
using UnityEngine;

namespace HuesNCues.Game
{
    /// <summary>
    /// Drives a local hotseat match. It owns the MatchController (the rules), listens
    /// to the BoardView (the grid) and updates a MatchHud (the view). All game rules
    /// live in Core; this class only translates clicks/buttons into Submit* calls and
    /// redraws when the match state changes (Observer pattern via StateChanged).
    ///
    /// Scene setup: put this on a GameObject, then assign BoardView, the MatchHud
    /// prefab and the PlayerMarker prefab in the inspector, and press Play.
    /// </summary>
    public class MatchView : MonoBehaviour
    {
        [SerializeField] private BoardView board;
        [SerializeField] private MatchHud hudPrefab;
        [SerializeField] private GameObject markerPrefab;
        [SerializeField] private int roundsPerMatch = 4;

        // For now the roster is fixed; a lobby/nickname screen comes in a later step.
        private static readonly (string Id, string Name)[] Roster =
        {
            ("p1", "Ana"), ("p2", "Bia"), ("p3", "Caio"),
        };

        private static readonly Color[] Palette =
        {
            new Color(0.90f, 0.25f, 0.25f),
            new Color(0.25f, 0.45f, 0.90f),
            new Color(0.25f, 0.70f, 0.35f),
            new Color(0.85f, 0.65f, 0.15f),
        };

        private ColorBoard _colorBoard;
        private MatchController _match;
        private MatchHud _hud;
        private List<Player> _players;
        private readonly Dictionary<string, Color> _playerColors = new Dictionary<string, Color>();
        private int _lastRound;

        private void Start()
        {
            if (board == null || hudPrefab == null)
            {
                Debug.LogError("MatchView needs a BoardView and a MatchHud prefab assigned.");
                enabled = false;
                return;
            }

            _colorBoard = board.Board;

            _hud = Instantiate(hudPrefab);
            ((RectTransform)_hud.transform).SetParent(board.Canvas.transform, false);
            _hud.SubmitClueRequested += OnSubmitClue;
            _hud.NextRequested += OnNext;

            board.CellClicked += OnCellClicked;
            StartNewMatch();
        }

        private void OnDestroy()
        {
            if (board != null) board.CellClicked -= OnCellClicked;
            if (_match != null) _match.StateChanged -= Refresh;
            if (_hud != null)
            {
                _hud.SubmitClueRequested -= OnSubmitClue;
                _hud.NextRequested -= OnNext;
            }
        }

        // ----- Match lifecycle ------------------------------------------------------

        private void StartNewMatch()
        {
            board.ClearMarkers();
            board.ClearTargetHighlight();

            _players = Roster.Select(r => new Player(r.Id, r.Name)).ToList();
            for (int i = 0; i < _players.Count; i++)
                _playerColors[_players[i].Id] = Palette[i % Palette.Length];

            if (_match != null) _match.StateChanged -= Refresh;
            _match = new MatchController(_players, _colorBoard, roundsPerMatch);
            _match.StateChanged += Refresh;

            _lastRound = 0;
            _match.StartMatch(); // fires StateChanged -> Refresh
        }

        // ----- Input handlers -------------------------------------------------------

        private void OnCellClicked(GridCoordinate coord)
        {
            var guesser = CurrentGuesser();
            if (guesser == null) return; // not a guessing phase

            if (_match.SubmitGuess(guesser.Id, coord))
                board.PlaceMarker(markerPrefab, coord, _playerColors[guesser.Id], Initial(guesser));
        }

        private void OnSubmitClue()
        {
            var cue = _match.CueMaster;
            if (cue == null) return;
            if (_match.SubmitClue(cue.Id, _hud.ClueText))
                _hud.ClueText = string.Empty;
        }

        private void OnNext()
        {
            if (_match.Phase == MatchPhase.Finished) StartNewMatch();
            else _match.NextRound();
        }

        // ----- Redraw (driven by MatchController.StateChanged) ----------------------

        private void Refresh()
        {
            if (_lastRound != _match.RoundNumber)
            {
                board.ClearMarkers();
                board.ClearTargetHighlight();
                _lastRound = _match.RoundNumber;
            }

            var phase = _match.Phase;
            bool cluePhase = phase == MatchPhase.CueMasterClue1 || phase == MatchPhase.CueMasterClue2;

            _hud.ShowClueControls(cluePhase);
            _hud.ShowSecret(cluePhase);
            _hud.ShowNext(phase == MatchPhase.Reveal || phase == MatchPhase.Finished);

            if (cluePhase)
            {
                _hud.SetSecretColor(board.ColorOf(_match.Target));
                _hud.ClueText = string.Empty;
            }
            if (phase == MatchPhase.Reveal) board.ShowTarget(_match.Target);
            _hud.SetNextLabel(phase == MatchPhase.Finished ? "Play Again" : "Next Round");

            _hud.SetStatus(BuildStatus(phase));
            _hud.SetScoreboard(BuildScoreboard());
        }

        private string BuildStatus(MatchPhase phase)
        {
            int r = _match.RoundNumber, tot = _match.TotalRounds;
            string cue = _match.CueMaster != null ? _match.CueMaster.Name : "";
            var guesser = CurrentGuesser();
            string who = guesser != null ? guesser.Name : "";

            switch (phase)
            {
                case MatchPhase.CueMasterClue1:
                    return $"Round {r}/{tot}  ·  Cue Master: {cue}  ·  Type a 1-word clue for the secret color.";
                case MatchPhase.Guessing1:
                    return $"Round {r}/{tot}  ·  Clue: \"{_match.Clue1}\"  ·  {who}, click a cell to guess.";
                case MatchPhase.CueMasterClue2:
                    return $"Round {r}/{tot}  ·  Cue Master: {cue}  ·  Type your 2nd clue word.";
                case MatchPhase.Guessing2:
                    return $"Round {r}/{tot}  ·  Clues: \"{_match.Clue1}\" \"{_match.Clue2}\"  ·  {who}, click your 2nd guess.";
                case MatchPhase.Reveal:
                    return $"The color was \"{board.NameOf(_match.Target)}\" ({_match.Target.Label}).  Scores updated — press Next Round.";
                case MatchPhase.Finished:
                    return $"Game over! Winner: {WinnerName()}.  Press Play Again.";
                default:
                    return "";
            }
        }

        private string BuildScoreboard()
        {
            var sb = new System.Text.StringBuilder("<b>SCORES</b>\n");
            foreach (var p in _players.OrderByDescending(p => p.Score))
            {
                bool isCue = _match.CueMaster != null && p.Id == _match.CueMaster.Id;
                sb.AppendLine($"{(isCue ? "★ " : "   ")}{p.Name}: {p.Score}");
            }
            return sb.ToString();
        }

        private string WinnerName()
        {
            var best = _players.OrderByDescending(p => p.Score).First();
            return $"{best.Name} ({best.Score})";
        }

        // ----- Helpers --------------------------------------------------------------

        private Player CurrentGuesser()
        {
            IReadOnlyDictionary<string, GridCoordinate> dict =
                _match.Phase == MatchPhase.Guessing1 ? _match.FirstGuesses :
                _match.Phase == MatchPhase.Guessing2 ? _match.SecondGuesses : null;
            if (dict == null) return null;
            return _match.Guessers.FirstOrDefault(p => !dict.ContainsKey(p.Id));
        }

        private static string Initial(Player p) =>
            string.IsNullOrEmpty(p.Name) ? "?" : p.Name.Substring(0, 1).ToUpperInvariant();
    }
}
