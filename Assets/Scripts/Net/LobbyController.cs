using System.Text;
using HuesNCues.Game;
using UnityEngine;

namespace HuesNCues.Net
{
    /// <summary>
    /// Drives the in-room lobby: shows the room code + connected players, and the
    /// host's Start button. Visible only while in a session and before the match
    /// starts; it reads the roster from MatchNetwork and reacts to its LobbyChanged /
    /// StateChanged events.
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        [SerializeField] private LobbyHud hudPrefab;
        [SerializeField] private SessionBootstrap session;
        [SerializeField] private MatchNetwork match;
        [SerializeField] private BoardView board;

        private LobbyHud _hud;

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
            _hud.StartClicked += OnStart;
            _hud.LeaveClicked += OnLeave;

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
                _hud.StartClicked -= OnStart;
                _hud.LeaveClicked -= OnLeave;
            }
        }

        private void OnStart() => match.HostStartMatch();
        private void OnLeave() => session.Leave();

        private void Refresh()
        {
            bool show = match.InLobby; // connected (spawned) and no match running yet
            _hud.SetVisible(show);
            if (!show) return;

            _hud.SetCode(string.IsNullOrEmpty(session.JoinCode) ? "" : $"Room code: {session.JoinCode}");

            var roster = match.CurrentLobby;
            int count = 0;
            var sb = new StringBuilder();
            if (roster?.names != null)
                for (int i = 0; i < roster.names.Length; i++)
                {
                    // Show each player in their assigned colour (after host de-duplication).
                    int colorIndex = (roster.colorIndexes != null && i < roster.colorIndexes.Length)
                        ? roster.colorIndexes[i] : 0;
                    sb.AppendLine($"• <color=#{HuesNCues.Core.PlayerPalette.Hex(colorIndex)}>{roster.names[i]}</color>");
                    count++;
                }
            _hud.SetPlayerList(sb.ToString());

            _hud.SetStartVisible(match.IsHost);
            _hud.SetStartInteractable(match.IsHost && count >= 2);
            _hud.SetStatus(match.IsHost
                ? (count >= 2 ? "Ready when you are." : "Waiting for players (need 2+)…")
                : "Waiting for the host to start…");
        }
    }
}
