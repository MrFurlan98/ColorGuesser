using System.Collections.Generic;
using ColorGuesser.Game;
using UnityEngine;

namespace ColorGuesser.Net
{
    /// <summary>
    /// Drives the in-room lobby: the room code (+ copy to clipboard), the player grid,
    /// and the main action button - "Iniciar Partida" for the host, "Pronto" for
    /// everyone else. Visible only while connected and before the match starts.
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        [SerializeField] private LobbyHud hudPrefab;
        [SerializeField] private SessionBootstrap session;
        [SerializeField] private MatchNetwork match;
        [SerializeField] private BoardView board;

        private LobbyHud _hud;
        private readonly List<LobbyPlayerInfo> _players = new List<LobbyPlayerInfo>();

        private void Start()
        {
            if (hudPrefab == null || session == null || match == null || board == null)
            {
                Debug.LogError("LobbyController needs hudPrefab, session, match and board assigned (run Set Up Networking).");
                enabled = false;
                return;
            }

            _hud = Instantiate(hudPrefab);
            ((RectTransform)_hud.transform).SetParent(board.Canvas.transform, false);
            _hud.ActionClicked += OnAction;
            _hud.LeaveClicked += OnLeave;
            _hud.CopyCodeClicked += OnCopyCode;
            _hud.SettingsChanged += OnSettingsChanged;

            session.Changed += Refresh;
            match.LobbyChanged += Refresh;
            match.StateChanged += Refresh;

            Refresh();
        }

        private void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
            if (match != null)
            {
                match.LobbyChanged -= Refresh;
                match.StateChanged -= Refresh;
            }
            if (_hud != null)
            {
                _hud.ActionClicked -= OnAction;
                _hud.LeaveClicked -= OnLeave;
                _hud.CopyCodeClicked -= OnCopyCode;
                _hud.SettingsChanged -= OnSettingsChanged;
            }
        }

        // ----- Actions --------------------------------------------------------------

        private void OnAction()
        {
            if (match.IsHost) match.HostStartMatch();   // host starts the match
            else match.SetLocalReady(!match.LocalReady); // everyone else toggles ready
        }

        private void OnLeave() => session.Leave();

        private void OnSettingsChanged(int maxPlayers, int targetScore, int guessSeconds)
        {
            if (match.IsHost) match.SetSettings(maxPlayers, targetScore, guessSeconds);
        }

        private void OnCopyCode()
        {
            // Not GUIUtility.systemCopyBuffer: that does nothing in a WebGL build, so the
            // button appeared to work in the editor and quietly failed in the browser.
            Clipboard.Copy(session.JoinCode);
        }

        // ----- Redraw ---------------------------------------------------------------

        private void Refresh()
        {
            bool show = match.InLobby; // connected (spawned) and no match running yet
            _hud.SetVisible(show);
            if (!show) return;

            var roster = match.CurrentLobby;
            BuildPlayerList(roster);
            _hud.SetPlayers(_players);

            // Room settings: only the host edits them, but everyone - the host included -
            // shows the synced values. Without this the host's widgets start on their own
            // first option while the real settings say something else, and the two only
            // agree once the host happens to touch a control.
            var settings = match.Settings;
            _hud.SetSettingsInteractable(match.IsHost);
            _hud.SetSettings(settings.maxPlayers, settings.targetScore, settings.guessSeconds);
            _hud.SetPlayerCount(_players.Count, settings.maxPlayers);

            bool hasCode = !string.IsNullOrEmpty(session.JoinCode);
            _hud.SetCode(hasCode ? session.JoinCode : "");
            _hud.SetCopyVisible(hasCode);

            if (match.IsHost)
            {
                // Enabled only with enough players and everyone marked ready.
                _hud.SetActionLabel("INICIAR PARTIDA");
                _hud.SetActionInteractable(_players.Count >= match.MinPlayersToStart &&
                                           match.EveryoneReady);
            }
            else
            {
                _hud.SetActionLabel(match.LocalReady ? "CANCELAR" : "PRONTO");
                _hud.SetActionInteractable(true);
            }
        }

        private void BuildPlayerList(LobbyRoster roster)
        {
            _players.Clear();
            if (roster?.names == null) return;

            for (int i = 0; i < roster.names.Length; i++)
            {
                bool isHost = roster.clientIds != null && i < roster.clientIds.Length &&
                              roster.clientIds[i] == roster.hostId;
                _players.Add(new LobbyPlayerInfo
                {
                    Name = roster.names[i],
                    ColorIndex = (roster.colorIndexes != null && i < roster.colorIndexes.Length)
                        ? roster.colorIndexes[i] : 0,
                    IsHost = isHost,
                    IsReady = isHost || (roster.ready != null && i < roster.ready.Length && roster.ready[i]),
                });
            }
        }
    }
}
