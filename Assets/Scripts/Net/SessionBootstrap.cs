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

        private string _joinCode = "";
        private string _status = "Not connected";
        private string _hostCode = "";
        private bool _busy;
        private ISession _session;

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

        private async void Host()
        {
            if (_busy) return;
            _busy = true;
            _status = "Creating session…";
            try
            {
                await EnsureSignedInAsync();
                var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                _hostCode = _session.Code;
                _status = "Hosting";
            }
            catch (Exception e)
            {
                _status = "Host failed: " + e.Message;
                Debug.LogException(e);
            }
            finally { _busy = false; }
        }

        private async void Join(string code)
        {
            if (_busy || string.IsNullOrWhiteSpace(code)) return;
            _busy = true;
            _status = "Joining…";
            try
            {
                await EnsureSignedInAsync();
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim());
                _status = "Joined";
            }
            catch (Exception e)
            {
                _status = "Join failed: " + e.Message;
                Debug.LogException(e);
            }
            finally { _busy = false; }
        }

        private async void Leave()
        {
            try { if (_session != null) await _session.LeaveAsync(); }
            catch (Exception e) { Debug.LogException(e); }
            _session = null;
            _hostCode = "";
            _status = "Not connected";
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 300, 220), GUI.skin.box);
            GUILayout.Label("Online (Relay)");
            GUILayout.Label($"Status: {_status}");

            if (_session == null)
            {
                GUI.enabled = !_busy;
                if (GUILayout.Button("Host (create session)")) Host();
                GUILayout.Space(6);
                GUILayout.Label("Join code:");
                _joinCode = GUILayout.TextField(_joinCode);
                if (GUILayout.Button("Join by code")) Join(_joinCode);
                GUI.enabled = true;
            }
            else
            {
                if (!string.IsNullOrEmpty(_hostCode))
                    GUILayout.Label($"Share this code:\n<b>{_hostCode}</b>");
                if (GUILayout.Button("Leave")) Leave();
            }

            GUILayout.EndArea();
        }
    }
}
