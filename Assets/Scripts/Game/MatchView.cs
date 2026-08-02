using System.Collections.Generic;
using System.Linq;
using ColorGuesser.Core;
using UnityEngine;

namespace ColorGuesser.Game
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
        [Tooltip("Points needed to win (offline games). Rounds continue until reached.")]
        [SerializeField] private int targetScore = 25;

        private IMatchSession _session;
        private MatchHud _hud;
        private GridCoordinate? _pendingGuess;   // picked on the board, not confirmed yet
        private readonly List<GuessStatusInfo> _guessStatuses = new List<GuessStatusInfo>();
        private readonly List<RoundScoreInfo> _roundScores = new List<RoundScoreInfo>();
        private readonly List<FinalScoreInfo> _finalScores = new List<FinalScoreInfo>();
        private bool _votedNextRound;            // this client already pressed next
        private bool _waitingForHost;            // non-host pressed play again on the end screen

        // Which round/phase the clue field was last emptied for, so typing is not wiped.
        private int _clueFieldRound = -1;
        private MatchPhase _clueFieldPhase = MatchPhase.NotStarted;

        /// <summary>Raised when the player asks to go back to the main menu. The
        /// networking layer subscribes and leaves the session.</summary>
        public event System.Action LeaveRequested;

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
            // If the HUD prefab reserves a slot for the board, put the board in it.
            board.SetBoardContainer(_hud.BoardContainer);
            _hud.SubmitClueRequested += OnSubmitClue;
            _hud.ConfirmGuessRequested += OnConfirmGuess;
            _hud.NextRoundVoteRequested += OnVoteNextRound;
            _hud.PlayAgainRequested += OnPlayAgain;
            _hud.MenuRequested += OnMenu;

            board.CellClicked += OnCellClicked;

            // The menu/lobby shows first; a game reveals the board + HUD.
            _hud.SetVisible(false);
            board.SetBoardVisible(false);
        }

        private void OnDestroy()
        {
            if (board != null) board.CellClicked -= OnCellClicked;
            if (_session != null) _session.StateChanged -= Refresh;
            if (_hud != null)
            {
                _hud.SubmitClueRequested -= OnSubmitClue;
                _hud.ConfirmGuessRequested -= OnConfirmGuess;
                _hud.NextRoundVoteRequested -= OnVoteNextRound;
                _hud.PlayAgainRequested -= OnPlayAgain;
                _hud.MenuRequested -= OnMenu;
            }
        }

        // The countdown changes every frame, so it is driven here rather than by the
        // state-changed event (which only fires on phase/score changes).
        private void Update()
        {
            if (_session == null || _hud == null) return;

            bool timed = MatchController.IsTimedPhase(_session.Match.Phase);
            _hud.SetTimer(timed ? _session.PhaseSecondsLeft : 0f,
                          timed ? _session.PhaseSecondsTotal : 0f);
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
            BindInternal(new LocalMatchSession(DefaultRoster.Create(), board.Board, targetScore));
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
            if (!CanGuessNow()) return;

            // Clicking only PICKS a cell; it is sent when the player confirms. The
            // marker appears on the board only after confirming.
            _pendingGuess = coord;
            ShowCoord(coord);   // preview the picked colour + code in the shared display
            _hud.SetGuessConfirmEnabled(true);
        }

        private void OnConfirmGuess()
        {
            if (_session == null || !_pendingGuess.HasValue || !CanGuessNow()) return;

            var guesser = _session.LocalPlayerId != null
                ? _session.Match.Players.FirstOrDefault(p => p.Id == _session.LocalPlayerId)
                : CurrentGuesser(); // hotseat: whoever is up next
            if (guesser == null) return;

            _session.Send(new SubmitGuessCommand { PlayerId = guesser.Id, Coord = _pendingGuess.Value });
            ClearPendingGuess();
        }

        /// <summary>True if this client may pick a cell right now.</summary>
        private bool CanGuessNow()
        {
            var m = _session.Match;
            if (m.Phase != MatchPhase.Guessing1 && m.Phase != MatchPhase.Guessing2) return false;

            string me = _session.LocalPlayerId;
            if (me == null) return CurrentGuesser() != null; // hotseat

            if (m.CueMaster != null && m.CueMaster.Id == me) return false; // cue master cannot guess
            var dict = m.Phase == MatchPhase.Guessing1 ? m.FirstGuesses : m.SecondGuesses;
            return !dict.ContainsKey(me); // already locked in this phase
        }

        /// <summary>
        /// Decides what the shared colour display shows: the target at the reveal, the
        /// secret colour if you are the cue master, otherwise the cell you picked.
        /// </summary>
        private void RefreshColorDisplay(IReadOnlyMatch match, MatchPhase phase, bool cueMasterView)
        {
            if (phase == MatchPhase.Reveal)
            {
                ShowCoord(match.Target);
                return;
            }
            if (!MatchController.IsTimedPhase(phase))
            {
                _hud.ShowColor(false, Color.white, string.Empty);
                return;
            }

            if (cueMasterView) ShowCoord(match.Target);              // only the cue master
            else if (_pendingGuess.HasValue) ShowCoord(_pendingGuess.Value);
            else _hud.ShowColor(false, Color.white, string.Empty);
        }

        private void ShowCoord(GridCoordinate coord) =>
            _hud.ShowColor(true, board.ColorOf(coord), coord.Label);

        private void ClearPendingGuess()
        {
            _pendingGuess = null;
            _hud.ShowColor(false, Color.white, string.Empty);
            _hud.SetGuessConfirmEnabled(false);
        }

        private void OnVoteNextRound()
        {
            if (_session == null || _session.Match.Phase != MatchPhase.Reveal) return;
            if (_votedNextRound) return;

            _votedNextRound = true;
            _session.VoteNextRound();
            Refresh(); // reflect the press immediately (button greys out)
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

        /// <summary>
        /// Only the host can restart. Everyone else gets the button anyway, and pressing it
        /// marks them as waiting - the restart is not theirs to make, but a button that
        /// silently does nothing reads as broken.
        /// </summary>
        private void OnPlayAgain()
        {
            if (_session == null) return;

            if (!_session.IsHost)
            {
                _waitingForHost = true;
                Refresh();
                return;
            }

            _session.RequestRestart(); // offline: rebuild locally; online: host rebuilds for all
        }

        /// <summary>Leave the room. The networking layer listens and closes the session.</summary>
        private void OnMenu() => LeaveRequested?.Invoke();

        /// <summary>
        /// Clears everything that belongs to a single match, so the screens can be reused:
        /// hides the board and every panel, and forgets this client's per-match choices.
        /// Called whenever we are not in a running match (menu, lobby, between matches).
        /// </summary>
        private void ResetForNewMatch()
        {
            _pendingGuess = null;
            _votedNextRound = false;
            _waitingForHost = false;
            _clueFieldRound = -1;
            _clueFieldPhase = MatchPhase.NotStarted;

            _hud.SetVisible(false);
            _hud.ShowGameplay(false);
            _hud.ShowGameInfo(false);
            _hud.ShowFinalScreen(false);
            _hud.HideScorePanel();
            _hud.HideFinalScores();
            _hud.HideStats();
            _hud.ShowColor(false, Color.white, string.Empty);
            _hud.SetGuessConfirmEnabled(false);
            _hud.ClueText = string.Empty;

            board.SetBoardVisible(false);
            board.ClearMarkers();
            board.ClearTargetHighlight();
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
                // Back in the menu/lobby: hide everything and drop this match's state, so
                // the next match starts from a clean HUD instead of the old one's leftovers.
                ResetForNewMatch();
                return;
            }
            // The final screen replaces the round UI entirely: no board, phases or panels.
            bool playing = phase != MatchPhase.Finished;
            _hud.SetVisible(true);
            _hud.ShowGameplay(playing);
            board.SetBoardVisible(playing);

            // GameInfo and the reveal score panel are siblings: exactly one is on.
            _hud.ShowGameInfo(MatchController.IsTimedPhase(phase));

            bool cluePhase = phase == MatchPhase.CueMasterClue1 || phase == MatchPhase.CueMasterClue2;
            bool mayGiveClue = cluePhase && (me == null || amCue); // hotseat, or I'm the cue master

            RedrawMarkers(m);
            if (phase == MatchPhase.Reveal) board.ShowTarget(m.Target);
            else board.ClearTargetHighlight();

            bool decided = m.Players.Any(p => p.Score >= m.TargetScore); // someone hit the target
            _hud.SetClueControlsEnabled(mayGiveClue);
            // Empty the field when a NEW clue is being asked for - not on every redraw.
            // Refresh runs on any state change, so clearing unconditionally wiped whatever
            // the cue master had typed the moment anything else happened, and the clue then
            // went out empty (which the rules reject) or never went at all.
            if (mayGiveClue && (_clueFieldRound != m.RoundNumber || _clueFieldPhase != phase))
            {
                _clueFieldRound = m.RoundNumber;
                _clueFieldPhase = phase;
                _hud.ClueText = string.Empty;
            }
            // Round number + the phase title/subtitle, written from this player's view.
            // In hotseat there is no single local player, so the screen belongs to the
            // cue master while clues are being given and to the guessers otherwise.
            bool cueMasterView = me == null ? cluePhase : amCue;
            // Only the clue for the half of the round being played. The second clue replaces
            // the first, so the display resets when the round moves on and nobody reads the
            // board against a word that belonged to guesses already locked in.
            bool secondHalf = phase == MatchPhase.CueMasterClue2 || phase == MatchPhase.Guessing2;
            string currentClue = secondHalf ? m.Clue2 : m.Clue1;
            // Clues belong to the current round, so they hide once the match is over.
            _hud.SetClue(currentClue, visible: phase != MatchPhase.Finished);

            _hud.SetRound(m.RoundNumber, phase == MatchPhase.Finished);
            _hud.SetPhaseTexts(phase, cueMasterView);
            _hud.SetPhaseSteps(phase);
            // Clue panel for the cue master, guess panel for everyone else; both hidden
            // outside the playing phases (reveal / end of match).
            _hud.SetRolePanels(cueMasterView, MatchController.IsTimedPhase(phase));

            // A pending pick only survives while it is still legal to make it.
            if (!CanGuessNow() && _pendingGuess.HasValue) _pendingGuess = null;
            RefreshColorDisplay(m, phase, cueMasterView);
            _hud.SetGuessConfirmEnabled(_pendingGuess.HasValue);
            _hud.SetGuessPlayers(BuildGuessStatuses(m, phase));

            RefreshScorePanel(m, phase, decided);
            RefreshFinalScores(m, phase);
        }

        /// <summary>
        /// The reveal score panel: everyone's points for the round, highest first, and
        /// the shared "next round" button. Hidden outside the reveal.
        /// </summary>
        private void RefreshScorePanel(IReadOnlyMatch match, MatchPhase phase, bool decided)
        {
            if (phase != MatchPhase.Reveal)
            {
                _votedNextRound = false; // reset for the next reveal
                _hud.HideScorePanel();
                return;
            }

            _roundScores.Clear();
            foreach (var p in match.Players)
                _roundScores.Add(new RoundScoreInfo
                {
                    Name = p.Name,
                    ColorIndex = p.ColorIndex,
                    RoundScore = match.RoundScores.TryGetValue(p.Id, out var s) ? s : 0,
                    TotalScore = p.Score,
                });
            _roundScores.Sort((a, b) => b.RoundScore.CompareTo(a.RoundScore)); // highest first

            _hud.ShowScorePanel(_roundScores, decided, _votedNextRound);
        }

        /// <summary>
        /// The end-of-match scoreboard: every player by total score, highest first.
        /// Players on the same score share a position (1, 2, 2, 4 …).
        /// </summary>
        private void RefreshFinalScores(IReadOnlyMatch match, MatchPhase phase)
        {
            if (phase != MatchPhase.Finished)
            {
                _hud.ShowFinalScreen(false);
                _hud.HideFinalScores();
                _hud.HideStats();
                return;
            }

            _hud.ShowFinalScreen(true); // parent first, so its panels can be seen
            // The host's button always acts; everyone else's settles once pressed.
            _hud.ShowStats(BuildStats(match), !_session.IsHost && _waitingForHost);

            _finalScores.Clear();
            var ranked = match.Players.OrderByDescending(p => p.Score).ToList();

            int position = 0, lastScore = int.MinValue;
            for (int i = 0; i < ranked.Count; i++)
            {
                if (ranked[i].Score != lastScore)
                {
                    position = i + 1;             // ties keep the earlier position
                    lastScore = ranked[i].Score;
                }
                _finalScores.Add(new FinalScoreInfo
                {
                    Position = position,
                    Name = ranked[i].Name,
                    ColorIndex = ranked[i].ColorIndex,
                    Score = ranked[i].Score,
                });
            }

            _hud.ShowFinalScores(_finalScores);
        }

        /// <summary>
        /// Works out the end-of-match stats from the round history: the clue whose round
        /// produced the most points, the colour whose round produced the fewest, all the
        /// exact guesses added up, and how long the match took.
        /// </summary>
        private MatchStatsInfo BuildStats(IReadOnlyMatch match)
        {
            var history = match.History;
            var stats = new MatchStatsInfo
            {
                Rounds = history.Count,
                Players = match.Players.Count,
                Seconds = match.ElapsedSeconds,
            };

            RoundRecord best = null, hardest = null;
            foreach (var round in history)
            {
                stats.ExactGuesses += round.ExactGuesses;

                // A round with no clue at all (the cue master ran out of time) cannot
                // be the "best clue".
                if (!string.IsNullOrWhiteSpace(round.ClueText) &&
                    (best == null || round.TotalPoints > best.TotalPoints))
                    best = round;

                if (hardest == null || round.TotalPoints < hardest.TotalPoints)
                    hardest = round;
            }

            if (best != null) stats.BestClue = best.ClueText.ToUpperInvariant();
            if (hardest != null)
                stats.HardestColor = hardest.Target.Label;

            return stats;
        }

        /// <summary>Who has already locked a guess this phase, for the guess panel list.</summary>
        private List<GuessStatusInfo> BuildGuessStatuses(IReadOnlyMatch match, MatchPhase phase)
        {
            _guessStatuses.Clear();

            IReadOnlyDictionary<string, GridCoordinate> dict =
                phase == MatchPhase.Guessing1 ? match.FirstGuesses :
                phase == MatchPhase.Guessing2 ? match.SecondGuesses : null;

            foreach (var player in match.Guessers)
                _guessStatuses.Add(new GuessStatusInfo
                {
                    Name = player.Name,
                    ColorIndex = player.ColorIndex,
                    HasGuessed = dict != null && dict.ContainsKey(player.Id),
                });

            return _guessStatuses;
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

        // ----- Helpers --------------------------------------------------------------

        private Player CurrentGuesser()
        {
            var m = _session.Match;
            IReadOnlyDictionary<string, GridCoordinate> dict =
                m.Phase == MatchPhase.Guessing1 ? m.FirstGuesses :
                m.Phase == MatchPhase.Guessing2 ? m.SecondGuesses : null;
            if (dict == null) return null;
            return m.Guessers.FirstOrDefault(p => !dict.ContainsKey(p.Id));
        }

        private static Color ColorForPlayer(IReadOnlyMatch match, string playerId)
        {
            // Each player carries the colour they picked in the menu (host-deduplicated).
            var player = match.Players.FirstOrDefault(p => p.Id == playerId);
            return player != null ? PlayerPalette.Get(player.ColorIndex) : Color.white;
        }

        private static string Initial(Player p) =>
            string.IsNullOrEmpty(p.Name) ? "?" : p.Name.Substring(0, 1).ToUpperInvariant();
    }
}
