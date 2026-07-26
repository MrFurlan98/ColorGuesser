using Unity.Netcode;
using UnityEngine;

namespace HuesNCues.Net
{
    /// <summary>
    /// A minimal connectivity harness for Netcode for GameObjects: it draws Host /
    /// Client / Server buttons (immediate-mode GUI, so no UI setup) and shows the
    /// current role and connected-client count. This is just to prove the transport
    /// works locally; the real game wiring comes in the next step.
    ///
    /// Add it via Tools > Hues N Cues > Set Up Networking (which also creates a
    /// configured NetworkManager). Then test with Multiplayer Play Mode.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
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
    }
}
