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
        public const string GuestKey = "guestMode";
        public const string ProfileKey = "profileId";

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
        /// Play as a guest: sign in with a throwaway account so nothing is kept between
        /// sessions, and skip saving any history or statistics. Off means the player keeps
        /// a stable account on this device and their stats are stored.
        /// </summary>
        public bool GuestMode
        {
            get => PlayerPrefs.GetInt(GuestKey, 0) == 1;
            set { PlayerPrefs.SetInt(GuestKey, value ? 1 : 0); Changed?.Invoke(); }
        }

        /// <summary>
        /// This player's Authentication id: stable across sessions unless they are a
        /// guest. Used as the player's identity in a match and as the Cloud Save key.
        /// Empty until signed in.
        /// </summary>
        public string PlayerId =>
            AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn
                ? AuthenticationService.Instance.PlayerId
                : "";

        /// <summary>False for guests, who asked not to have their data stored.</summary>
        public bool CanStoreData => !GuestMode;

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

            SetStatus("Not connected");
            // Losing the connection is not the same as leaving: without this we stay a
            // member of the room on the service, and the next join is refused.
            ReleaseSessionQuietly();
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

        private bool _signedInAsGuest;

        private async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            var auth = AuthenticationService.Instance;
            bool guest = GuestMode;

            // Already signed in, but under the other kind of account: start over, or the
            // player would keep the identity they just opted out of (or into).
            if (auth.IsSignedIn && _signedInAsGuest != guest)
                auth.SignOut();

            if (auth.IsSignedIn) return;

            // The profile decides WHICH cached anonymous account is used. A guest gets a
            // fresh one every time, so nothing of theirs is ever reused; everyone else
            // reuses this device's profile, which is what makes their stats persist.
            try { auth.SwitchProfile(guest ? NewGuestProfile() : StableProfile()); }
            catch (Exception e) { Debug.LogWarning("Could not switch profile: " + e.Message); }

            await auth.SignInAnonymouslyAsync();
            _signedInAsGuest = guest;

            // Anonymous accounts have no visible name, so without this there is no way to
            // tell which row in the Cloud Save dashboard belongs to this player.
            Debug.Log($"Signed in {(guest ? "as guest" : "with the saved profile")}. " +
                      $"PlayerId: {auth.PlayerId}");
        }

        private static string NewGuestProfile() =>
            "guest" + Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>
        /// A profile name kept on this device, so signing in again lands on the same
        /// anonymous account (and therefore the same saved data).
        ///
        /// In the editor it is kept per process instead. Multiplayer Play Mode virtual
        /// players share this machine's PlayerPrefs, so one stored profile would sign every
        /// instance into the SAME account - and they would then be one player as far as the
        /// services are concerned: the second to join a room is refused because that player
        /// is already in it. A process-scoped name keeps them distinct while staying stable
        /// across play mode, which is what testing reconnection needs.
        /// </summary>
        private static string StableProfile()
        {
#if UNITY_EDITOR
            return "editor" + System.Diagnostics.Process.GetCurrentProcess().Id;
#else
            string id = PlayerPrefs.GetString(ProfileKey, "");
            if (string.IsNullOrEmpty(id))
            {
                id = "player" + Guid.NewGuid().ToString("N").Substring(0, 8);
                PlayerPrefs.SetString(ProfileKey, id);
                PlayerPrefs.Save();
            }
            return id;
#endif
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
            if (_busy) return;
            _busy = true;
            Notice = "";
            try { await ReleaseSessionAsync(); }
            finally { _busy = false; SetStatus("Not connected"); }
        }

        /// <summary>
        /// Gives the session up properly. Both halves matter: dropping our reference without
        /// telling the service still leaves us registered as a member of that room, and the
        /// next attempt to join is then refused because that player is already in it.
        /// Safe to call when there is nothing to release.
        /// </summary>
        private async Task ReleaseSessionAsync()
        {
            var session = _session;
            _session = null;                    // nobody should see a room we are abandoning
            if (session == null) return;

            _leaving = true;   // so the disconnect this causes is not reported as a fault
            try { await session.LeaveAsync(); }
            catch (Exception e)
            {
                // Being gone already - the host closed the room, or the connection dropped -
                // is normal here, not a failure worth showing the player.
                Debug.LogWarning("Leaving the session did not complete cleanly: " + e.Message);
            }
            finally
            {
                _leaving = false;
                var manager = NetworkManager.Singleton;
                if (manager != null && manager.IsListening) manager.Shutdown();
            }
        }

        /// <summary>Releases the session after the room ended without us.</summary>
        private async void ReleaseSessionQuietly()
        {
            _busy = true;
            try { await ReleaseSessionAsync(); }
            finally { _busy = false; Changed?.Invoke(); }
        }

    }
}
