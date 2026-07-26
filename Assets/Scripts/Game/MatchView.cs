using System.Linq;
using HuesNCues.Core;
using UnityEngine;

namespace HuesNCues.Game
{
    /// <summary>
    /// Drives a match through an IMatchSession (the seam): reads state from
    /// session.Match, listens to StateChanged, sends commands. It works unchanged for
    /// a local (hotseat) session and a networked one.
    ///
    /// Ownership: session.LocalPlayerId tells us which player this client controls.
    ///   - null  -> hotseat: one screen drives every player in turn.
    ///   - set   -> multiplayer: you may only act as your own player, and the secret
    ///              color is shown only to the cue master.
    ///
    /// Markers are rendered FROM state (not on click), so local and networked drawing
    /// are identical.
    /// </summary>
    public class MatchView : MonoBehaviour
    {
        [SerializeField] private BoardView board;
        [SerializeField] private MatchHud hudPrefab;
        [SerializeField] private GameObject markerPrefab;
        [SerializeField] private int roundsPerMatch = 4;

        private static readonly Color[] Palette =
        {
            new Color(0.90f, 0.25f, 0.25f),
            new Color(0.25f, 0.45f, 0.90f),
            new Color(0.25f, 0.70f, 0.35f),
            new Color(0.85f, 0.65f, 0.15f),
        };

        private IMatchSession _session;
        private MatchHud _hud;

        private void Start()
        {
            if (board == null || hudPrefab == null)
            {
                Debug.LogError("MatchView needs a BoardView and a MatchHud prefab assigned.");
                enabled = false;
                return;
            }

            _hud = Instantiate(hudPrefab);
            ((RectTransform)_hud.transform).SetParent(board.Canvas.transform, false);
            _hud.SubmitClueRequested += OnSubmitClue;
            _hud.NextRequested += OnNext;

            board.CellClicked += OnCellClicked;

            _hud.SetVisible(false); // the menu/lobby shows first; a game reveals the HUD
        }

        private void OnDestroy()
        {
            if (board != null) board.CellClicked -= OnCellClicked;
            if (_session != null) _session.StateChanged -= Refresh;
            if (_hud != null)
            {
                _hud.SubmitClueRequested -= OnSubmitClue;
                _hud.NextRequested -= OnNext;
            }
        }

        // ----- Session binding ------------------------------------------------------

        /// <summary>Binds an external (e.g. networked) session. The HUD reveals itself
        /// once the match actually starts (Refresh handles visibility by phase).</summary>
        public void Bind(IMatchSession session)
        {
            BindInternal(session);
            Refresh();
        }

        /// <summary>Starts a local hotseat match (chosen from the menu's "Play Offline").</summary>
        public void StartHotseat()
        {
            BindInternal(new LocalMatchSession(DefaultRoster.Create(), board.Board, roundsPerMatch));
            _session.Start(); // fires StateChanged -> Refresh, which shows the HUD
        }

        private void BindInternal(IMatchSession session)
        {
            if (_session != null) _session.StateChanged -= Refresh;
            _session = session;
            _session.StateChanged += Refresh;
        }

        // ----- Input -> commands ----------------------------------------------------

        private void OnCellClicked(GridCoordinate coord)
        {
            if (_session == null) return; // menu is up, no match yet
            var m = _session.Match;
            string me = _session.LocalPlayerId;

            if (me == null) // hotseat: guess for whoever is up next
            {
                var g = CurrentGuesser();
                if (g != null) _session.Send(new SubmitGuessCommand { PlayerId = g.Id, Coord = coord });
                return;
            }

            // Multiplayer: only guess as myself, only if I'm a guesser who hasn't guessed.
            if (m.Phase != MatchPhase.Guessing1 && m.Phase != MatchPhase.Guessing2) return;
            if (m.CueMaster != null && m.CueMaster.Id == me) return;
            var dict = m.Phase == MatchPhase.Guessing1 ? m.FirstGuesses : m.SecondGuesses;
            if (dict.ContainsKey(me)) return;

            _session.Send(new SubmitGuessCommand { PlayerId = me, Coord = coord });
        }

        private void OnSubmitClue()
        {
            if (_session == null) return;
            var cue = _session.Match.CueMaster;
            if (cue == null) return;

            string me = _session.LocalPlayerId;
            if (me != null && me != cue.Id) return; // only the cue master client may submit

            _session.Send(new SubmitClueCommand { PlayerId = cue.Id, Word = _hud.ClueText });
        }

        private void OnNext()
        {
            if (_session == null) return;
            var m = _session.Match;
            if (m.Phase == MatchPhase.Finished)
            {
                _session.RequestRestart(); // offline: rebuild local match; online: host rebuilds for all
                return;
            }
            _session.Send(new NextRoundCommand { PlayerId = _session.LocalPlayerId ?? m.CueMaster?.Id });
        }

        // ----- Redraw (driven by StateChanged) --------------------------------------

        private void Refresh()
        {
            var m = _session.Match;
            var phase = m.Phase;
            string me = _session.LocalPlayerId;
            bool amCue = me != null && m.CueMaster != null && m.CueMaster.Id == me;

            if (phase == MatchPhase.NotStarted)
            {
                // In the menu/lobby: keep the game HUD hidden entirely.
                _hud.SetVisible(false);
                board.ClearMarkers();
                board.ClearTargetHighlight();
                return;
            }
            _hud.SetVisible(true);

            bool cluePhase = phase == MatchPhase.CueMasterClue1 || phase == MatchPhase.CueMasterClue2;
            bool mayGiveClue = cluePhase && (me == null || amCue); // hotseat, or I'm the cue master

            RedrawMarkers(m);
            if (phase == MatchPhase.Reveal) board.ShowTarget(m.Target);
            else board.ClearTargetHighlight();

            _hud.ShowClueControls(mayGiveClue);
            _hud.ShowSecret(mayGiveClue); // only the cue master sees the secret color
            _hud.ShowNext(phase == MatchPhase.Reveal || phase == MatchPhase.Finished);

            if (mayGiveClue)
            {
                _hud.SetSecretColor(board.ColorOf(m.Target));
                _hud.ClueText = string.Empty;
            }
            _hud.SetNextLabel(phase == MatchPhase.Finished ? "Play Again" : "Next Round");
            _hud.SetStatus(BuildStatus(m, phase, me, amCue));
            _hud.SetScoreboard(BuildScoreboard(m));
        }

        private void RedrawMarkers(IReadOnlyMatch match)
        {
            board.ClearMarkers();
            foreach (var kv in match.FirstGuesses) PlaceGuessMarker(match, kv.Key, kv.Value);
            foreach (var kv in match.SecondGuesses) PlaceGuessMarker(match, kv.Key, kv.Value);
        }

        private void PlaceGuessMarker(IReadOnlyMatch match, string playerId, GridCoordinate coord)
        {
            var player = match.Players.FirstOrDefault(p => p.Id == playerId);
            board.PlaceMarker(markerPrefab, coord, ColorForPlayer(match, playerId), player != null ? Initial(player) : "?");
        }

        // ----- Status / scoreboard text ---------------------------------------------

        private string BuildStatus(IReadOnlyMatch m, MatchPhase phase, string me, bool amCue)
        {
            int r = m.RoundNumber, tot = m.TotalRounds;
            string cue = m.CueMaster != null ? m.CueMaster.Name : "";

            // Reveal / Finished read the same for everyone.
            if (phase == MatchPhase.Reveal)
                return $"The color was \"{board.NameOf(m.Target)}\" ({m.Target.Label}).  Scores updated — press Next Round.";
            if (phase == MatchPhase.Finished)
                return $"Game over! Winner: {WinnerName(m)}.  Press Play Again.";

            if (me == null) // hotseat wording
            {
                string who = CurrentGuesser()?.Name ?? "";
                switch (phase)
                {
                    case MatchPhase.CueMasterClue1: return $"Round {r}/{tot}  ·  Cue Master: {cue}  ·  Type a 1-word clue.";
                    case MatchPhase.CueMasterClue2: return $"Round {r}/{tot}  ·  Cue Master: {cue}  ·  Type your 2nd clue word.";
                    case MatchPhase.Guessing1: return $"Round {r}/{tot}  ·  Clue: \"{m.Clue1}\"  ·  {who}, click a cell.";
                    case MatchPhase.Guessing2: return $"Round {r}/{tot}  ·  Clues: \"{m.Clue1}\" \"{m.Clue2}\"  ·  {who}, 2nd guess.";
                    default: return "";
                }
            }

            // Multiplayer wording (from this player's perspective).
            bool clue1 = phase == MatchPhase.CueMasterClue1;
            bool guessing = phase == MatchPhase.Guessing1 || phase == MatchPhase.Guessing2;
            string clues = phase == MatchPhase.Guessing2 ? $"\"{m.Clue1}\" \"{m.Clue2}\"" : $"\"{m.Clue1}\"";

            if (clue1 || phase == MatchPhase.CueMasterClue2)
                return amCue
                    ? $"Round {r}/{tot}  ·  You are the Cue Master. Type {(clue1 ? "a" : "your 2nd")} clue word."
                    : $"Round {r}/{tot}  ·  Waiting for {cue} to give {(clue1 ? "a" : "the 2nd")} clue…";

            if (guessing)
            {
                if (amCue) return $"Round {r}/{tot}  ·  Clue: {clues}  ·  Waiting for guesses…";
                var dict = phase == MatchPhase.Guessing1 ? m.FirstGuesses : m.SecondGuesses;
                return dict.ContainsKey(me)
                    ? $"Round {r}/{tot}  ·  Clue: {clues}  ·  Waiting for other players…"
                    : $"Round {r}/{tot}  ·  Clue: {clues}  ·  Your turn — click a cell!";
            }

            return "";
        }

        private static string BuildScoreboard(IReadOnlyMatch match)
        {
            var sb = new System.Text.StringBuilder("<b>SCORES</b>\n");
            foreach (var p in match.Players.OrderByDescending(p => p.Score))
            {
                bool isCue = match.CueMaster != null && p.Id == match.CueMaster.Id;
                sb.AppendLine($"{(isCue ? "★ " : "   ")}{p.Name}: {p.Score}");
            }
            return sb.ToString();
        }

        private static string WinnerName(IReadOnlyMatch match)
        {
            var best = match.Players.OrderByDescending(p => p.Score).FirstOrDefault();
            return best != null ? $"{best.Name} ({best.Score})" : "-";
        }

        // ----- Helpers --------------------------------------------------------------

        private Player CurrentGuesser()
        {
            var m = _session.Match;
            System.Collections.Generic.IReadOnlyDictionary<string, GridCoordinate> dict =
                m.Phase == MatchPhase.Guessing1 ? m.FirstGuesses :
                m.Phase == MatchPhase.Guessing2 ? m.SecondGuesses : null;
            if (dict == null) return null;
            return m.Guessers.FirstOrDefault(p => !dict.ContainsKey(p.Id));
        }

        private static Color ColorForPlayer(IReadOnlyMatch match, string playerId)
        {
            var players = match.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i].Id == playerId) return Palette[i % Palette.Length];
            return Color.white;
        }

        private static string Initial(Player p) =>
            string.IsNullOrEmpty(p.Name) ? "?" : p.Name.Substring(0, 1).ToUpperInvariant();
    }
}
