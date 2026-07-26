using System;
using System.Threading.Tasks;
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
        [SerializeField] private int maxPlayers = 4;

        public const string NicknameKey = "nickname";

        private string _status = "Not connected";
        private string _hostCode = "";
        private string _nickname = "";
        private bool _busy;
        private ISession _session;

        // Public API for the menu UI (MenuController) to drive.
        public string Status => _status;
        public string JoinCode => _hostCode;
        public bool IsBusy => _busy;
        public bool InSession => _session != null;
        public string Nickname
        {
            get => _nickname;
            set { _nickname = value ?? ""; PlayerPrefs.SetString(NicknameKey, _nickname); }
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
                _hostCode = _session.Code;
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
            _hostCode = "";
            SetStatus("Not connected");
        }

    }
}
