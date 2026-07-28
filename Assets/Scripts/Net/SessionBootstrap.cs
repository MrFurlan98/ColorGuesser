using System;
using System.Threading.Tasks;
using HuesNCues.Core;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace HuesNCues.Net
{
    /// <summary>
    /// Relay-backed connection using the Multiplayer Services SDK (Sessions API).
    /// The host creates a session and gets a short join code; clients join by code.
    /// The session integrates with Netcode automatically (WithRelayNetwork starts the
    /// NGO host/client through Relay), so our MatchNetwork flow is unchanged - real
    /// remote play and WebGL now work through the same seam.
    ///
    /// This is a temporary immediate-mode panel; the polished lobby comes later.
    /// </summary>
    public class SessionBootstrap : MonoBehaviour
    {
        [Tooltip("Hard cap for the Relay session. The host's lobby dropdown chooses the " +
                 "room size within this, so keep it at the highest allowed value.")]
        [SerializeField] private int maxPlayers = 10;

        public const string NicknameKey = "nickname";
        public const string ColorKey = "colorIndex";

        private string _status = "Not connected";
        private string _nickname = "";
        private bool _busy;
        private ISession _session;

        // Public API for the menu UI (MenuController) to drive.
        public string Status => _status;

        /// <summary>The room code. Read from the session itself, so clients who joined
        /// see it too (not just the host who created it).</summary>
        public string JoinCode => _session != null ? _session.Code : "";
        public bool IsBusy => _busy;
        public bool InSession => _session != null;

        /// <summary>Maximum players allowed in a room (shown as "2/4" in the lobby).</summary>
        public int MaxPlayers => maxPlayers;
        public string Nickname
        {
            get => _nickname;
            set { _nickname = value ?? ""; PlayerPrefs.SetString(NicknameKey, _nickname); }
        }

        /// <summary>Chosen PlayerPalette index. The host may reassign it if two players
        /// pick the same colour (first to enter keeps it).</summary>
        public int ColorIndex
        {
            get => PlayerPalette.Clamp(PlayerPrefs.GetInt(ColorKey, 0));
            set => PlayerPrefs.SetInt(ColorKey, PlayerPalette.Clamp(value));
        }

        /// <summary>Raised whenever the connection status/session changes.</summary>
        public event Action Changed;

        private void Awake() => _nickname = PlayerPrefs.GetString(NicknameKey, "Player");

        private void SetStatus(string s) { _status = s; Changed?.Invoke(); }

        private async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                // A unique profile per instance keeps Multiplayer Play Mode virtual
                // players (and repeat runs) from sharing one anonymous account.
                try { AuthenticationService.Instance.SwitchProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 8)); }
                catch { /* SwitchProfile is best-effort */ }

                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        public async void Host()
        {
            if (_busy || InSession) return;
            _busy = true;
            SetStatus("Creating session…");
            try
            {
                await EnsureSignedInAsync();
                var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                SetStatus("Hosting");
            }
            catch (Exception e)
            {
                SetStatus("Host failed: " + e.Message);
                Debug.LogException(e);
            }
            finally { _busy = false; Changed?.Invoke(); }
        }

        public async void Join(string code)
        {
            if (_busy || InSession || string.IsNullOrWhiteSpace(code)) return;
            _busy = true;
            SetStatus("Joining…");
            try
            {
                await EnsureSignedInAsync();
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim());
                SetStatus("Joined");
            }
            catch (Exception e)
            {
                SetStatus("Join failed: " + e.Message);
                Debug.LogException(e);
            }
            finally { _busy = false; Changed?.Invoke(); }
        }

        public async void Leave()
        {
            try { if (_session != null) await _session.LeaveAsync(); }
            catch (Exception e) { Debug.LogException(e); }
            _session = null;
            SetStatus("Not connected");
        }

    }
}
