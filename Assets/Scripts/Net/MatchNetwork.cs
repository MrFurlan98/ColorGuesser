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
    ///   - Each connected client is one player, identified by their Authentication id
    ///     (not the network client id, which changes on every reconnect and means nothing
    ///     between sessions). Clients report theirs when they join.
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

        [Tooltip("Optional. Saves this player's lifetime stats when a match ends. " +
                 "Skipped for guests.")]
        [SerializeField] private PlayerStatsStore statsStore;
        [Tooltip("Fallback settings; the host overrides these from the lobby dropdowns.")]
        [SerializeField] private LobbySettings defaultSettings = new LobbySettings();

        private MatchController _serverMatch;                              // server only
        private readonly SnapshotMatch _clientMatch = new SnapshotMatch(); // client only
        private readonly SnapshotMatch _emptyMatch = new SnapshotMatch();  // before the match starts

        private readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();  // server: clientId -> nickname
        private readonly Dictionary<ulong, int> _colors = new Dictionary<ulong, int>();       // server: clientId -> palette index
        private readonly Dictionary<ulong, bool> _ready = new Dictionary<ulong, bool>();      // server: clientId -> ready
        private readonly Dictionary<ulong, string> _authIds = new Dictionary<ulong, string>(); // server: clientId -> Authentication id
        private LobbyRoster _lobby = new LobbyRoster                                          // shown in the lobby
        {
            clientIds = new long[0], names = new string[0], colorIndexes = new int[0],
            ready = new bool[0], hostId = -1,
        };

        [Tooltip("Seconds the reveal waits before moving on by itself.")]
        [SerializeField] private float revealSeconds = 15f;

        [Tooltip("Players needed to start in a BUILD. The game is designed for 3+ (the " +
                 "scoring rules have a special case for exactly 3). In the editor the " +
                 "minimum drops to 2 so a match can be tested with two virtual players.")]
        [SerializeField] private int minPlayersToStart = 3;

        private LobbySettings _settings;      // host-chosen; mirrored to clients via the roster
        private float _guessDeadline = -1f;   // local countdown for the current guessing phase
        private MatchPhase _lastPhase = MatchPhase.NotStarted;
        private bool _resultRecorded;         // this match's result is already saved

        private readonly HashSet<ulong> _nextVotes = new HashSet<ulong>(); // server: ready for next round
        private float _revealDeadline = -1f;
        private int _votes;                   // mirrored to clients for the "2/4" display

        public IReadOnlyMatch Match =>
            IsServer ? (_serverMatch != null ? (IReadOnlyMatch)_serverMatch : _emptyMatch) : _clientMatch;

        public event Action StateChanged;

        /// <summary>
        /// My identity in the match: my Authentication id, or null if I am not one of the
        /// players. Falls back to the network client id when there is no account id, which
        /// is what PlayerIdFor does on the host.
        /// </summary>
        public string LocalPlayerId
        {
            get
            {
                if (NetworkManager == null) return null;

                string authId = session != null ? session.PlayerId : "";
                string fallback = NetworkManager.LocalClientId.ToString();

                foreach (var p in Match.Players)
                    if ((!string.IsNullOrEmpty(authId) && p.Id == authId) || p.Id == fallback)
                        return p.Id;
                return null;
            }
        }

        // ----- Lobby API (for the lobby UI) -----------------------------------------

        public LobbyRoster CurrentLobby => _lobby;
        public event Action LobbyChanged;
        public bool IsHost => IsServer; // also satisfies IMatchSession.IsHost
        public bool InLobby => IsSpawned && Match.Phase == MatchPhase.NotStarted;
        public void HostStartMatch() { if (IsServer) StartMatchServer(); }

        /// <summary>
        /// Players needed before the host can start (the lobby greys out below this).
        /// Relaxed to 2 in the editor so a match can be exercised with two virtual
        /// players; builds use the configured value.
        /// </summary>
        public int MinPlayersToStart =>
#if UNITY_EDITOR
            Mathf.Min(2, minPlayersToStart);
#else
            minPlayersToStart;
#endif

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
                if (_lobby?.clientIds == null || _lobby.clientIds.Length < MinPlayersToStart)
                    return false;
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

        /// <summary>
        /// The largest room the host may choose. Capped by what the Relay session was
        /// actually created with: offering more seats than the session holds means the
        /// lobby promises places that do not exist, and the extra players are turned away
        /// by the service before this class ever sees them.
        /// </summary>
        private int MaxRoomSize => session != null ? Mathf.Max(3, session.MaxPlayers) : 10;

        /// <summary>Host only: change the room settings and sync them to everyone.</summary>
        public void SetSettings(int maxPlayers, int targetScore, int guessSeconds)
        {
            if (!IsServer) return;
            _settings = new LobbySettings
            {
                maxPlayers = Mathf.Clamp(maxPlayers, 3, MaxRoomSize),
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

                // Back in the lobby, so the next match is a new one to record.
                if (phase == MatchPhase.NotStarted) _resultRecorded = false;

                // The match ended: each peer saves its own result, once. The flag is what
                // makes it once - not the phase we came from. Snapshots are applied outside
                // Update, so a client can go from mid-round straight to Finished in one
                // frame without ever being seen in the reveal, and that match still counts.
                if (phase == MatchPhase.Finished && !_resultRecorded)
                {
                    _resultRecorded = true;
                    RecordOwnResult();
                }
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

            // Tell the host my nickname, colour and account id (picked in the menu).
            string myName = PlayerPrefs.GetString(SessionBootstrap.NicknameKey, "Player");
            int myColor = PlayerPrefs.GetInt(SessionBootstrap.ColorKey, 0);
            string myAuthId = session != null ? session.PlayerId : "";
            SetProfileRpc(Encoding.UTF8.GetBytes(myName), myColor, Encoding.UTF8.GetBytes(myAuthId));
        }

        private void OnLeaveRequested()
        {
            if (session != null) session.Leave();
        }

        /// <summary>
        /// Saves this player's own result at the end of a match. Only their aggregate is
        /// written, and only by them - nobody stores anyone else's data. Skipped for
        /// guests, and for a spectator who was not part of the match.
        /// </summary>
        private void RecordOwnResult()
        {
            if (statsStore == null) return;

            string me = LocalPlayerId;
            if (string.IsNullOrEmpty(me)) return;

            var match = Match;
            var player = match.Players.FirstOrDefault(p => p.Id == me);
            if (player == null) return;

            int best = match.Players.Max(p => p.Score);
            bool won = player.Score == best;

            // Fire and forget: a storage hiccup must never hold up the end screen.
            _ = statsStore.RecordMatchAsync(player.Score, won, match.History.Count);
        }

        public override void OnNetworkDespawn()
        {
            if (matchView != null) matchView.LeaveRequested -= OnLeaveRequested;
            if (_serverMatch != null)
            {
                _serverMatch.StateChanged -= OnServerMatchChanged;
                _serverMatch = null;
            }
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            // Forget the match we just left. Without this the view still reads the last
            // state it was given - the final screen - and redraws it over the menu, so
            // leaving looks like the button did nothing. Restarting never hit this because
            // it broadcasts an empty snapshot first; leaving had no equivalent.
            _clientMatch.Apply(MatchSnapshot.Capture(_emptyMatch));
            _lastPhase = MatchPhase.NotStarted;
            _resultRecorded = false;

            // Per-connection bookkeeping, so joining another room does not inherit any of
            // it. Client ids restart from zero, so stale entries would be read as the new
            // players' names, colours and accounts.
            _names.Clear();
            _colors.Clear();
            _ready.Clear();
            _authIds.Clear();
            _nextVotes.Clear();
            _votes = 0;
            _guessDeadline = -1f;
            _revealDeadline = -1f;

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
                .Select((id, i) => new Player(PlayerIdFor(id), NameFor(id, i), ColorFor(id)))
                .ToList();
            if (players.Count < MinPlayersToStart)
            {
                Debug.LogWarning($"Need at least {MinPlayersToStart} connected players to start.");
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

            // Enforce the room size the host chose. The Relay session is created with the
            // largest allowed cap, so this is what actually holds a room to, say, 6.
            int capacity = Settings != null ? Settings.maxPlayers : int.MaxValue;
            if (clientId != NetworkManager.LocalClientId &&
                NetworkManager.ConnectedClientsIds.Count > capacity)
            {
                Debug.Log($"Room is full ({capacity}); disconnecting client {clientId}.");
                NetworkManager.DisconnectClient(clientId, "A sala está cheia.");
                return;
            }

            BroadcastLobby();
            // Always sync the current state to (re)connecting clients - even when no
            // match is running - so they land on the lobby instead of a stale game.
            IReadOnlyMatch current = _serverMatch != null ? (IReadOnlyMatch)_serverMatch : _emptyMatch;
            SnapshotRpc(MatchSnapshot.Capture(current).ToBytes());
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            // Work out who they were BEFORE forgetting their account id - PlayerIdFor
            // falls back to the client id once _authIds no longer has them, and the match
            // is keyed on the account id, so dropping them would silently miss.
            string playerId = PlayerIdFor(clientId);

            _names.Remove(clientId);
            _colors.Remove(clientId); // frees their colour for the next player
            _ready.Remove(clientId);
            _authIds.Remove(clientId);
            BroadcastLobby();

            if (_serverMatch == null) return;

            // A player left mid-match. Drop them from the round rather than ending the
            // match: they keep their score on the board and their seat, and play carries
            // on without waiting for them. If they come back, TryReseat puts them in it.
            _serverMatch.DropPlayer(playerId);

            // Below the minimum there is no game left to play, so back to the lobby.
            if (_serverMatch.ConnectedCount < MinPlayersToStart)
            {
                Debug.Log("Too few players left to continue; returning to the lobby.");
                ReturnToLobby();
            }
        }

        [Rpc(SendTo.Server)]
        private void SetProfileRpc(byte[] nameUtf8, int requestedColor, byte[] authIdUtf8,
            RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            string authId = authIdUtf8 != null ? Encoding.UTF8.GetString(authIdUtf8) : "";
            if (!string.IsNullOrEmpty(authId)) _authIds[sender] = authId;

            DropStaleConnectionsFor(sender, authId);

            // This is the first moment we know WHO connected - the account id does not
            // exist yet at OnClientConnected - so it is where a returning player is
            // recognised and given their seat back.
            if (TryReseat(sender, authId)) return;

            _names[sender] = Encoding.UTF8.GetString(nameUtf8);
            _colors[sender] = ResolveColor(sender, requestedColor);
            BroadcastLobby();
        }

        /// <summary>
        /// One account, one connection. If this account is already in the room under a
        /// different client id, that connection is the stale one and gets dropped.
        ///
        /// Two cases lead here. A player reconnecting faster than the transport notices
        /// they went away - the old connection is already dead, we just have not been told
        /// yet. And the same account genuinely opening the game twice, where letting both
        /// in would be worse than picking one: both would answer to the same player id, so
        /// either could act as the other, and the round's guesses (keyed on that id) would
        /// only ever record one of them while waiting for two.
        ///
        /// Their account id is forgotten first, so the disconnect this triggers cannot mark
        /// the seat absent that the new connection is about to take over.
        /// </summary>
        private void DropStaleConnectionsFor(ulong clientId, string authId)
        {
            if (string.IsNullOrEmpty(authId)) return;

            var stale = _authIds
                .Where(kv => kv.Key != clientId && kv.Value == authId)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var old in stale)
            {
                _authIds.Remove(old);
                NetworkManager.DisconnectClient(old, "Sua conta entrou nesta sala em outro lugar.");
            }
        }

        /// <summary>
        /// Reconnect: if this account already holds a seat in the running match, put them
        /// back in it rather than treating them as a new arrival. They keep their score,
        /// their place in the cue master rotation, and the name and colour they were playing
        /// with - changing those mid-match would make the scoreboard unreadable for everyone
        /// else.
        ///
        /// Guests are never recognised: a fresh throwaway account every time is the whole
        /// point of guest mode, so there is nothing to match them against.
        /// </summary>
        private bool TryReseat(ulong clientId, string authId)
        {
            if (_serverMatch == null || string.IsNullOrEmpty(authId)) return false;

            var seat = _serverMatch.Players.FirstOrDefault(p => p.Id == authId);
            if (seat == null) return false;   // not one of the players; a spectator

            // A no-op when the seat was never marked absent, which happens if they got back
            // before the transport noticed they had gone.
            _serverMatch.RejoinPlayer(authId);

            _names[clientId] = seat.Name;
            _colors[clientId] = seat.ColorIndex;

            Debug.Log($"{seat.Name} reconnected; seat restored with {seat.Score} point(s).");

            BroadcastLobby();
            // Unconditional, because RejoinPlayer only broadcasts when it changed something.
            SnapshotRpc(MatchSnapshot.Capture(_serverMatch).ToBytes());
            return true;
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

        /// <summary>
        /// A player's identity in the match. This is their Authentication id, not their
        /// network client id: the client id changes every time they reconnect and means
        /// nothing between sessions, whereas the account id is stable and is what saved
        /// data is keyed on. Falls back to the client id if the account id never arrived.
        /// </summary>
        private string PlayerIdFor(ulong clientId) =>
            _authIds.TryGetValue(clientId, out var id) && !string.IsNullOrEmpty(id)
                ? id
                : clientId.ToString();

        /// <summary>
        /// Is this player id one of the sender's own? Accepts either their account id or
        /// their client id, because a roster built before their account id arrived would
        /// hold the client id - and rejecting them then would silently break their turn.
        /// It still refuses ids belonging to anybody else, which is the point of the check.
        /// </summary>
        private bool IsSender(ulong clientId, string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            if (playerId == clientId.ToString()) return true;
            return _authIds.TryGetValue(clientId, out var authId) && playerId == authId;
        }

        private void BroadcastLobby()
        {
            if (!IsServer) return;

            // Clamped here too, not only in SetSettings: the fallback settings are edited
            // in the inspector and have no idea what the session was created with.
            var effective = (_settings ?? defaultSettings).Clone();
            effective.maxPlayers = Mathf.Clamp(effective.maxPlayers, 3, MaxRoomSize);

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
                settings = effective,
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
            if (!IsSender(rpcParams.Receive.SenderClientId, dto.playerId)) return;
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
