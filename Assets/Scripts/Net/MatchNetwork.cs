using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColorGuesser.Core;
using ColorGuesser.Game;
using Unity.Netcode;
using UnityEngine;

namespace ColorGuesser.Net
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
    /// Lives on an in-scene NetworkObject (created by Tools > Adivinhe a Cor > Set Up
    /// Networking) with references to the scene's BoardView and MatchView.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class MatchNetwork : NetworkBehaviour, IMatchSession
    {
        [SerializeField] private BoardView board;
        [SerializeField] private MatchView matchView;

        [Tooltip("Used to leave the room when the player asks to return to the menu.")]
        [SerializeField] private SessionBootstrap session;
        [Tooltip("Fallback settings; the host overrides these from the lobby dropdowns.")]
        [SerializeField] private LobbySettings defaultSettings = new LobbySettings();

        private MatchController _serverMatch;                              // server only
        private readonly SnapshotMatch _clientMatch = new SnapshotMatch(); // client only
        private readonly SnapshotMatch _emptyMatch = new SnapshotMatch();  // before the match starts

        private readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();  // server: clientId -> nickname
        private readonly Dictionary<ulong, int> _colors = new Dictionary<ulong, int>();       // server: clientId -> palette index
        private readonly Dictionary<ulong, bool> _ready = new Dictionary<ulong, bool>();      // server: clientId -> ready
        private LobbyRoster _lobby = new LobbyRoster                                          // shown in the lobby
        {
            clientIds = new long[0], names = new string[0], colorIndexes = new int[0],
            ready = new bool[0], hostId = -1,
        };

        [Tooltip("Seconds the reveal waits before moving on by itself.")]
        [SerializeField] private float revealSeconds = 15f;

        private LobbySettings _settings;      // host-chosen; mirrored to clients via the roster
        private float _guessDeadline = -1f;   // local countdown for the current guessing phase
        private MatchPhase _lastPhase = MatchPhase.NotStarted;

        private readonly HashSet<ulong> _nextVotes = new HashSet<ulong>(); // server: ready for next round
        private float _revealDeadline = -1f;
        private int _votes;                   // mirrored to clients for the "2/4" display

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

        /// <summary>Is this client marked ready? (The host is always considered ready.)</summary>
        public bool LocalReady
        {
            get
            {
                if (NetworkManager == null || _lobby?.clientIds == null) return false;
                long me = (long)NetworkManager.LocalClientId;
                for (int i = 0; i < _lobby.clientIds.Length; i++)
                    if (_lobby.clientIds[i] == me)
                        return _lobby.ready != null && i < _lobby.ready.Length && _lobby.ready[i];
                return false;
            }
        }

        /// <summary>True when every non-host player has marked themselves ready.</summary>
        public bool EveryoneReady
        {
            get
            {
                if (_lobby?.clientIds == null || _lobby.clientIds.Length < 2) return false;
                for (int i = 0; i < _lobby.clientIds.Length; i++)
                {
                    if (_lobby.clientIds[i] == _lobby.hostId) continue; // host doesn't ready up
                    if (_lobby.ready == null || i >= _lobby.ready.Length || !_lobby.ready[i]) return false;
                }
                return true;
            }
        }

        /// <summary>The room settings everyone currently sees (host-chosen).</summary>
        public LobbySettings Settings => _lobby?.settings ?? _settings ?? defaultSettings;

        /// <summary>Host only: change the room settings and sync them to everyone.</summary>
        public void SetSettings(int maxPlayers, int targetScore, int guessSeconds)
        {
            if (!IsServer) return;
            _settings = new LobbySettings
            {
                maxPlayers = Mathf.Clamp(maxPlayers, 3, 10),
                targetScore = Mathf.Max(1, targetScore),
                guessSeconds = Mathf.Max(0, guessSeconds),
            };
            BroadcastLobby();
        }

        /// <summary>Seconds left in the current timed phase (0 when untimed).</summary>
        public float PhaseSecondsLeft =>
            _guessDeadline < 0f ? 0f : Mathf.Max(0f, _guessDeadline - Time.time);

        /// <summary>Length of a timed phase, for the progress bar (0 when untimed).</summary>
        public float PhaseSecondsTotal =>
            _guessDeadline < 0f ? 0f : Mathf.Max(0, Settings != null ? Settings.guessSeconds : 0);

        /// <summary>Marks this client ready / not ready (ignored for the host).</summary>
        public void SetLocalReady(bool ready) => SetReadyRpc(ready);

        // ----- Reveal: everyone votes to move on -------------------------------------

        public int NextRoundVotes => _votes;
        public int NextRoundVotesNeeded =>
            NetworkManager != null ? NetworkManager.ConnectedClientsIds.Count : 0;

        public float NextRoundSecondsLeft =>
            _revealDeadline < 0f ? 0f : Mathf.Max(0f, _revealDeadline - Time.time);

        public void VoteNextRound() => VoteNextRoundRpc();

        [Rpc(SendTo.Server)]
        private void VoteNextRoundRpc(RpcParams rpcParams = default)
        {
            if (_serverMatch == null || _serverMatch.Phase != MatchPhase.Reveal) return;

            _nextVotes.Add(rpcParams.Receive.SenderClientId);
            SyncVotesRpc(_nextVotes.Count);
            _votes = _nextVotes.Count;

            if (_nextVotes.Count >= NetworkManager.ConnectedClientsIds.Count) AdvanceFromReveal();
        }

        [Rpc(SendTo.NotServer)]
        private void SyncVotesRpc(int votes)
        {
            _votes = votes;
            StateChanged?.Invoke();
        }

        private void AdvanceFromReveal()
        {
            _nextVotes.Clear();
            _votes = 0;
            _revealDeadline = -1f;
            SyncVotesRpc(0);
            _serverMatch.NextRound();
        }

        /// <summary>
        /// Drives the round timer. It applies equally to the clue and guessing phases,
        /// restarting on each one. Every peer tracks its own countdown for display, but
        /// only the server actually ends the phase, so the host stays authoritative.
        /// </summary>
        private void Update()
        {
            if (!IsSpawned) return;

            var phase = Match.Phase;
            bool timed = MatchController.IsTimedPhase(phase);
            int limit = Settings != null ? Settings.guessSeconds : 0;

            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                _guessDeadline = (timed && limit > 0) ? Time.time + limit : -1f;

                // Entering the reveal starts the "everyone ready" window; leaving clears it.
                _revealDeadline = phase == MatchPhase.Reveal && revealSeconds > 0f
                    ? Time.time + revealSeconds : -1f;
                if (phase != MatchPhase.Reveal && IsServer) { _nextVotes.Clear(); _votes = 0; }
            }

            // The reveal moves on by itself once its window expires.
            if (phase == MatchPhase.Reveal)
            {
                if (_revealDeadline >= 0f && Time.time >= _revealDeadline)
                {
                    _revealDeadline = -1f;
                    if (IsServer && _serverMatch != null) AdvanceFromReveal();
                }
                return;
            }

            if (!timed || _guessDeadline < 0f) return;
            if (Time.time < _guessDeadline) return;

            _guessDeadline = -1f;
            if (IsServer && _serverMatch != null) _serverMatch.ForceAdvancePhase();
        }

        [Rpc(SendTo.Server)]
        private void SetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            _ready[rpcParams.Receive.SenderClientId] = ready;
            BroadcastLobby();
        }

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
            matchView.LeaveRequested += OnLeaveRequested;

            // Tell the host my chosen nickname + colour (picked in the menu).
            string myName = PlayerPrefs.GetString(SessionBootstrap.NicknameKey, "Player");
            int myColor = PlayerPrefs.GetInt(SessionBootstrap.ColorKey, 0);
            SetProfileRpc(Encoding.UTF8.GetBytes(myName), myColor);
        }

        private void OnLeaveRequested()
        {
            if (session != null) session.Leave();
        }

        public override void OnNetworkDespawn()
        {
            if (matchView != null) matchView.LeaveRequested -= OnLeaveRequested;
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

            // "Jogar novamente" sends everyone back to the lobby rather than straight
            // into a new match, so players can ready up (and change colours) again.
            ReturnToLobby();
        }

        /// <summary>
        /// Ends the current match and puts everyone back in the lobby, with every
        /// non-host player marked not ready again.
        /// </summary>
        private void ReturnToLobby()
        {
            if (!IsServer) return;

            if (_serverMatch != null)
            {
                _serverMatch.StateChanged -= OnServerMatchChanged;
                _serverMatch = null;
            }

            _nextVotes.Clear();
            _votes = 0;
            _revealDeadline = -1f;
            _ready.Clear();            // everyone has to say they are ready again

            BroadcastLobby();
            StateChanged?.Invoke();                                    // host UI -> lobby
            SnapshotRpc(MatchSnapshot.Capture(_emptyMatch).ToBytes()); // clients -> lobby
        }

        private void StartMatchServer()
        {
            if (!IsServer || _serverMatch != null) return;

            // Drop anything left over from the previous match so a rematch starts clean.
            _nextVotes.Clear();
            _votes = 0;
            _revealDeadline = -1f;
            SyncVotesRpc(0);

            var players = NetworkManager.ConnectedClientsIds
                .Select((id, i) => new Player(id.ToString(), NameFor(id, i), ColorFor(id)))
                .ToList();
            if (players.Count < 2)
            {
                Debug.LogWarning("Need at least 2 connected players to start.");
                return;
            }

            _serverMatch = new MatchController(players, board.Board, Settings.targetScore);
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
            _ready.Remove(clientId);
            BroadcastLobby();

            if (_serverMatch == null) return;

            // A player left mid-match. MatchController can't drop a player, so we send
            // everyone back to the lobby rather than stall on the missing player.
            ReturnToLobby();
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
        private int ResolveColor(ulong client, int requested) =>
            ColorAssignment.Resolve(requested,
                _colors.Where(kv => kv.Key != client).Select(kv => kv.Value));

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
                // The host counts as ready; everyone else opts in.
                ready = ids.Select(id => id == NetworkManager.LocalClientId ||
                                         (_ready.TryGetValue(id, out var r) && r)).ToArray(),
                hostId = (long)NetworkManager.LocalClientId,
                settings = (_settings ?? defaultSettings).Clone(),
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
