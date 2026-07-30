using ColorGuesser.Game;
using UnityEngine;

namespace ColorGuesser.Net
{
    /// <summary>
    /// Drives the main menu: instantiates the MenuHud prefab and connects it to the
    /// session (nickname, colour choice, Host, Join by code). The menu shows until we
    /// connect, then hides so the lobby and game can take over.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        [SerializeField] private MenuHud hudPrefab;
        [SerializeField] private SessionBootstrap session;
        [SerializeField] private MatchNetwork match;
        [SerializeField] private BoardView board;

        private MenuHud _hud;

        private void Start()
        {
            if (hudPrefab == null || session == null || match == null || board == null)
            {
                Debug.LogError("MenuController needs hudPrefab, session, match and board assigned (run Set Up Networking).");
                enabled = false;
                return;
            }

            _hud = Instantiate(hudPrefab);
            ((RectTransform)_hud.transform).SetParent(board.Canvas.transform, false);
            _hud.SetNickname(session.Nickname);
            _hud.ApplyPaletteColors();                 // tint the 10 swatches from PlayerPalette
            _hud.SetSelectedColor(session.ColorIndex); // restore last pick

            _hud.HostClicked += OnHost;
            _hud.JoinClicked += OnJoin;
            _hud.NicknameChanged += OnNicknameChanged;
            _hud.ColorChanged += OnColorChanged;
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
                _hud.NicknameChanged -= OnNicknameChanged;
                _hud.ColorChanged -= OnColorChanged;
            }
        }

        private void OnHost() => session.Host();
        private void OnJoin() => session.Join(_hud.JoinCode);
        private void OnNicknameChanged(string nickname) => session.Nickname = nickname;
        private void OnColorChanged(int colorIndex) => session.ColorIndex = colorIndex;

        private void Refresh()
        {
            // Menu is up until we connect (MatchNetwork spawns).
            bool showMenu = !match.IsSpawned;
            _hud.SetVisible(showMenu);
            if (!showMenu) return;

            _hud.SetInteractable(!session.IsBusy);
            // Explains a room we did not leave on purpose: host closed it, connection
            // lost, or the room was full.
            _hud.SetNotice(session.Notice);
        }
    }
}
