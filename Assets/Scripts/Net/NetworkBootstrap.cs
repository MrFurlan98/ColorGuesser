using Unity.Netcode;
using UnityEngine;

namespace HuesNCues.Net
{
    /// <summary>
    /// Developer-only connectivity harness for Netcode for GameObjects: Host / Client /
    /// Server buttons plus the connected-client count, drawn with immediate-mode GUI.
    /// Handy for testing a local connection without going through the menu and Relay.
    ///
    /// EDITOR ONLY - the panel is compiled out of builds, where players connect through
    /// the real menu instead.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            GUILayout.BeginArea(new Rect(590, 12, 240, 220), GUI.skin.box);

            if (nm == null)
            {
                GUILayout.Label("No NetworkManager in the scene.\nRun Tools > Hues N Cues > Set Up Networking.");
                GUILayout.EndArea();
                return;
            }

            if (!nm.IsClient && !nm.IsServer)
            {
                GUILayout.Label("Not connected");
                if (GUILayout.Button("Host")) nm.StartHost();
                if (GUILayout.Button("Client")) nm.StartClient();
                if (GUILayout.Button("Server")) nm.StartServer();
            }
            else
            {
                string role = nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client";
                GUILayout.Label($"Running as: {role}");
                if (nm.IsServer) GUILayout.Label($"Connected clients: {nm.ConnectedClients.Count}");
                if (GUILayout.Button("Shutdown")) nm.Shutdown();
            }

            GUILayout.EndArea();
        }
#endif
    }
}
