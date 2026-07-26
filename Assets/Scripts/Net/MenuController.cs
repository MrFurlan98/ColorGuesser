using HuesNCues.Game;
using UnityEngine;

namespace HuesNCues.Net
{
    /// <summary>
    /// Drives the main menu: instantiates the MenuHud prefab and connects its buttons
    /// to the session (Host / Join) and to the offline hotseat. The menu shows until
    /// you enter a session or start an offline game, then hides so the game (and the
    /// in-room lobby) can take over.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        [SerializeField] private MenuHud hudPrefab;
        [SerializeField] private SessionBootstrap session;
        [SerializeField] private MatchNetwork match;
        [SerializeField] private MatchView matchView;
        [SerializeField] private BoardView board;

        private MenuHud _hud;
        private bool _offlineStarted;

        private void Start()
        {
            if (hudPrefab == null || session == null || match == null || matchView == null || board == null)
            {
                Debug.LogError("MenuController needs hudPrefab, session, match, matchView and board assigned (run Set Up Networking).");
                enabled = false;
                return;
            }

            _hud = Instantiate(hudPrefab);
            ((RectTransform)_hud.transform).SetParent(board.Canvas.transform, false);
            _hud.SetNickname(session.Nickname);

            _hud.HostClicked += OnHost;
            _hud.JoinClicked += OnJoin;
            _hud.OfflineClicked += OnOffline;
            _hud.NicknameChanged += OnNicknameChanged;
            session.Changed += Refresh;
            match.LobbyChanged += Refresh; // fires on connect and on despawn (leave)

            Refresh();
        }

        private void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
            if (match != null) match.LobbyChanged -= Refresh;
            if (_hud != null)
            {
                _hud.HostClicked -= OnHost;
                _hud.JoinClicked -= OnJoin;
                _hud.OfflineClicked -= OnOffline;
                _hud.NicknameChanged -= OnNicknameChanged;
            }
        }

        private void OnHost() => session.Host();
        private void OnJoin() => session.Join(_hud.JoinCode);
        private void OnNicknameChanged(string nickname) => session.Nickname = nickname;

        private void OnOffline()
        {
            _offlineStarted = true;
            matchView.StartHotseat();
            Refresh();
        }

        private void Refresh()
        {
            // Menu is up until we connect (MatchNetwork spawns) or start an offline game.
            bool showMenu = !_offlineStarted && !match.IsSpawned;
            _hud.SetVisible(showMenu);
            if (showMenu)
            {
                _hud.SetStatus(session.Status);
                _hud.SetInteractable(!session.IsBusy);
            }
        }
    }
}
