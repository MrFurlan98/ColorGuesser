using System;
using System.Threading.Tasks;
using ColorGuesser.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace ColorGuesser.Net
{
    /// <summary>
    /// Relay-backed connection using the Multiplayer Services SDK (Sessions API).
    /// The host creates a session and gets a short join code; clients join by code.
    /// The session integrates with Netcode automatically (WithRelayNetwork starts the
    /// NGO host/client through Relay), so our MatchNetwork flow is unchanged - real
    /// remote play and WebGL now work through the same seam.
    ///
    /// It also watches for losing the connection, so the menu can explain what happened
    /// instead of the player simply finding themselves back at the start.
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

        /// <summary>
        /// Why the player was last dropped out of a room, for the menu to show - the host
        /// closing the session, a lost connection, or a full room. Empty when they left on
        /// purpose. Cleared once they connect again.
        /// </summary>
        public string Notice { get; private set; } = "";

        private bool _leaving;   // true while WE are the ones ending the session

        private void Awake() => _nickname = PlayerPrefs.GetString(NicknameKey, "Player");

        private bool _subscribed;

        // This component lives on the NetworkManager object, so the singleton may not be
        // assigned yet in OnEnable - hence the second attempt in Start.
        private void OnEnable() => SubscribeToDisconnects();
        private void Start() => SubscribeToDisconnects();

        private void SubscribeToDisconnects()
        {
            if (_subscribed) return;
            var manager = NetworkManager.Singleton;
            if (manager == null) return;

            manager.OnClientDisconnectCallback += OnClientDisconnect;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            var manager = NetworkManager.Singleton;
            if (manager != null) manager.OnClientDisconnectCallback -= OnClientDisconnect;
            _subscribed = false;
        }

        /// <summary>
        /// Fires on this peer when it loses the connection. If we did not ask to leave,
        /// the room ended without us - most often because the host closed it, which a
        /// host-authoritative session cannot survive.
        /// </summary>
        private void OnClientDisconnect(ulong clientId)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || _leaving) return;
            if (manager.IsServer || clientId != manager.LocalClientId) return; // someone else left

            string reason = manager.DisconnectReason;
            Notice = string.IsNullOrWhiteSpace(reason)
                ? "A conexão com a sala foi perdida."
                : reason;

            _session = null;
            SetStatus("Not connected");
        }

        private void SetStatus(string s) { _status = s; Changed?.Invoke(); }

        /// <summary>
        /// Makes Unity Transport agree with the protocol Relay will hand us.
        /// RelayProtocol.Default is secure WebSockets on WebGL (browsers cannot open raw
        /// UDP) and DTLS everywhere else - and UNITY_WEBGL is also defined in the Editor
        /// whenever the active build target is WebGL. If the transport's "Use WebSockets"
        /// setting disagrees with the allocation, it refuses to start.
        /// </summary>
        private static void MatchTransportToRelayProtocol()
        {
            var manager = NetworkManager.Singleton;
            var transport = manager != null ? manager.GetComponent<UnityTransport>() : null;
            if (transport == null) return;

            transport.UseWebSockets = RelayProtocol.Default == RelayProtocol.WSS;
        }

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
            Notice = "";
            SetStatus("Creating session…");
            try
            {
                await EnsureSignedInAsync();
                MatchTransportToRelayProtocol();
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
            Notice = "";
            SetStatus("Joining…");
            try
            {
                await EnsureSignedInAsync();
                MatchTransportToRelayProtocol();
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
            _leaving = true;   // so OnClientDisconnect does not report this as a fault
            Notice = "";
            try { if (_session != null) await _session.LeaveAsync(); }
            catch (Exception e) { Debug.LogException(e); }
            _session = null;
            _leaving = false;
            SetStatus("Not connected");
        }

    }
}
