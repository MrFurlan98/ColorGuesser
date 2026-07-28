using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HuesNCues.Core;
using HuesNCues.Game;
using Unity.Netcode;
using UnityEngine;

namespace HuesNCues.Net
{
    /// <summary>
    /// The Network Gateway: runs a match host-authoritatively over Netcode for
    /// GameObjects, and implements IMatchSession so MatchView drives it exactly like
    /// the local session.
    ///
    ///   - Each connected client is one player; the player's id IS its network client
    ///     id (as a string), so "which player am I" is just my LocalClientId.
    ///   - The host presses "Start Match" once everyone has joined; the match is built
    ///     from the connected clients.
    ///   - Server validates every command against the sender's id, so no client can
    ///     act as another player.
    ///
    /// Lives on an in-scene NetworkObject (created by Tools > Hues N Cues > Set Up
    /// Networking) with references to the scene's BoardView and MatchView.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class MatchNetwork : NetworkBehaviour, IMatchSession
    {
        [SerializeField] private BoardView board;
        [SerializeField] private MatchView matchView;
        [Tooltip("Points needed to win. Rounds continue until a player reaches this.")]
        [SerializeField] private int targetScore = 25;

        private MatchController _serverMatch;                              // server only
        private readonly SnapshotMatch _clientMatch = new SnapshotMatch(); // client only
        private readonly SnapshotMatch _emptyMatch = new SnapshotMatch();  // before the match starts

        private readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();  // server: clientId -> nickname
        private readonly Dictionary<ulong, int> _colors = new Dictionary<ulong, int>();       // server: clientId -> palette index
        private LobbyRoster _lobby = new LobbyRoster                                          // shown in the lobby
        {
            clientIds = new long[0], names = new string[0], colorIndexes = new int[0]
        };

        public IReadOnlyMatch Match =>
            IsServer ? (_serverMatch != null ? (IReadOnlyMatch)_serverMatch : _emptyMatch) : _clientMatch;

        public event Action StateChanged;

        /// <summary>My player id is my network client id (or null if I am not a player).</summary>
        public string LocalPlayerId
        {
            get
            {
                if (NetworkManager == null) return null;
                string me = NetworkManager.LocalClientId.ToString();
                foreach (var p in Match.Players)
                    if (p.Id == me) return me;
                return null;
            }
        }

        // ----- Lobby API (for the lobby UI) -----------------------------------------

        public LobbyRoster CurrentLobby => _lobby;
        public event Action LobbyChanged;
        public bool IsHost => IsServer; // also satisfies IMatchSession.IsHost
        public bool InLobby => IsSpawned && Match.Phase == MatchPhase.NotStarted;
        public void HostStartMatch() { if (IsServer) StartMatchServer(); }

        public override void OnNetworkSpawn()
        {
            if (board == null || matchView == null)
            {
                Debug.LogError("MatchNetwork needs BoardView and MatchView references (run Set Up Networking).");
                return;
            }

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
            matchView.Bind(this); // UI switches from its offline session to this one

            // Tell the host my chosen nickname + colour (picked in the menu).
            string myName = PlayerPrefs.GetString(SessionBootstrap.NicknameKey, "Player");
            int myColor = PlayerPrefs.GetInt(SessionBootstrap.ColorKey, 0);
            SetProfileRpc(Encoding.UTF8.GetBytes(myName), myColor);
        }

        public override void OnNetworkDespawn()
        {
            if (_serverMatch != null) _serverMatch.StateChanged -= OnServerMatchChanged;
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            // Let the menu/lobby refresh (we are no longer connected).
            LobbyChanged?.Invoke();
            StateChanged?.Invoke();
        }

        // ----- IMatchSession --------------------------------------------------------

        public void Start() { /* the host starts the match with the Start Match button */ }

        public void Send(IMatchCommand command)
        {
            var dto = CommandDto.From(command);
            if (dto != null) SubmitCommandRpc(dto.ToBytes());
        }

        public void RequestRestart() => RequestRestartRpc();

        [Rpc(SendTo.Server)]
        private void RequestRestartRpc(RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (rpcParams.Receive.SenderClientId != NetworkManager.LocalClientId) return; // host only

            if (_serverMatch != null)
            {
                _serverMatch.StateChanged -= OnServerMatchChanged;
                _serverMatch = null;
            }

            StartMatchServer(); // rebuild from the currently connected clients
            if (_serverMatch == null) // not enough players to restart -> back to waiting
            {
                StateChanged?.Invoke();
                SnapshotRpc(MatchSnapshot.Capture(_emptyMatch).ToBytes());
            }
        }

        private void StartMatchServer()
        {
            if (!IsServer || _serverMatch != null) return;

            var players = NetworkManager.ConnectedClientsIds
                .Select((id, i) => new Player(id.ToString(), NameFor(id, i), ColorFor(id)))
                .ToList();
            if (players.Count < 2)
            {
                Debug.LogWarning("Need at least 2 connected players to start.");
                return;
            }

            _serverMatch = new MatchController(players, board.Board, targetScore);
            _serverMatch.StateChanged += OnServerMatchChanged;
            _serverMatch.StartMatch();
        }

        private string NameFor(ulong clientId, int index)
        {
            return _names.TryGetValue(clientId, out var n) && !string.IsNullOrWhiteSpace(n)
                ? n
                : $"Player {index + 1}";
        }

        // ----- Server side ----------------------------------------------------------

        private void OnServerMatchChanged()
        {
            StateChanged?.Invoke();                                       // host's own UI
            SnapshotRpc(MatchSnapshot.Capture(_serverMatch).ToBytes());   // every client
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            BroadcastLobby();
            // Always sync the current state to (re)connecting clients - even when no
            // match is running - so they land on the lobby instead of a stale game.
            IReadOnlyMatch current = _serverMatch != null ? (IReadOnlyMatch)_serverMatch : _emptyMatch;
            SnapshotRpc(MatchSnapshot.Capture(current).ToBytes());
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            _names.Remove(clientId);
            _colors.Remove(clientId); // frees their colour for the next player
            BroadcastLobby();

            if (_serverMatch == null) return;

            // A player left mid-match. MatchController can't drop a player, so we abort
            // back to "waiting" rather than let the game stall on the missing player.
            _serverMatch.StateChanged -= OnServerMatchChanged;
            _serverMatch = null;
            StateChanged?.Invoke();                                       // host UI -> waiting
            SnapshotRpc(MatchSnapshot.Capture(_emptyMatch).ToBytes());    // clients -> waiting
        }

        [Rpc(SendTo.Server)]
        private void SetProfileRpc(byte[] nameUtf8, int requestedColor, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            _names[sender] = Encoding.UTF8.GetString(nameUtf8);
            _colors[sender] = ResolveColor(sender, requestedColor);
            BroadcastLobby();
        }

        /// <summary>
        /// First come, first served: a player keeps the colour they asked for unless
        /// somebody already holds it, in which case they get a random free one.
        /// </summary>
        private int ResolveColor(ulong client, int requested)
        {
            requested = PlayerPalette.Clamp(requested);

            bool taken = _colors.Any(kv => kv.Key != client && kv.Value == requested);
            if (!taken) return requested;

            var free = Enumerable.Range(0, PlayerPalette.Count)
                .Where(i => !_colors.Any(kv => kv.Key != client && kv.Value == i))
                .ToList();
            if (free.Count == 0) return requested; // more players than colours: allow a repeat

            return free[UnityEngine.Random.Range(0, free.Count)];
        }

        private int ColorFor(ulong clientId) =>
            _colors.TryGetValue(clientId, out var c) ? c : 0;

        private void BroadcastLobby()
        {
            if (!IsServer) return;

            var ids = NetworkManager.ConnectedClientsIds.ToArray();
            _lobby = new LobbyRoster
            {
                clientIds = ids.Select(id => (long)id).ToArray(),
                names = ids.Select((id, i) => NameFor(id, i)).ToArray(),
                colorIndexes = ids.Select(ColorFor).ToArray(),
            };
            LobbyChanged?.Invoke();       // host UI
            LobbyRpc(_lobby.ToBytes());   // clients
        }

        [Rpc(SendTo.NotServer)]
        private void LobbyRpc(byte[] json)
        {
            _lobby = LobbyRoster.FromBytes(json);
            LobbyChanged?.Invoke();
        }

        [Rpc(SendTo.Server)]
        private void SubmitCommandRpc(byte[] json, RpcParams rpcParams = default)
        {
            if (_serverMatch == null) return;
            var dto = CommandDto.FromBytes(json);
            if (dto == null) return;

            // Ownership: the command's player must be the client that sent it.
            if (dto.playerId != rpcParams.Receive.SenderClientId.ToString()) return;
            // Only the host advances rounds (command type 2 = NextRound).
            if (dto.type == 2 && rpcParams.Receive.SenderClientId != NetworkManager.LocalClientId) return;

            dto.ToCommand()?.ApplyTo(_serverMatch);
        }

        // ----- Client side ----------------------------------------------------------

        [Rpc(SendTo.NotServer)]
        private void SnapshotRpc(byte[] json)
        {
            _clientMatch.Apply(MatchSnapshot.FromBytes(json));
            StateChanged?.Invoke();
        }
    }
}
