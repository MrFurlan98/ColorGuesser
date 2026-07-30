# Known Issues

Backlog for the project, grouped by priority. Items marked **(doc)** are things the
proposal PDF promises that the build does not do yet — those are the ones most likely
to be noticed when the project is presented.

---

## High priority

### 1. No persistence at all **(doc)** — DONE and verified
**Done (2026-07-30):**
- **Stable player identity.** `SessionBootstrap` now picks the Authentication *profile*
  before signing in: a device-stable one (`player…`, kept in PlayerPrefs) so the same
  anonymous account — and therefore the same `PlayerId` — is reused on every visit. This
  is the identity #6's reconnect work also needs.
- **Guest mode.** A menu toggle switches to a throwaway profile (`guest…`, new every
  time), and nothing of that player is read or written. Switching between guest and
  normal signs out first, so nobody keeps the identity they just opted out of.
- **Cloud Save.** `PlayerStatsStore` (`Assets/Scripts/Net/PlayerStatsStore.cs`) reads and
  writes one record per player under the key `playerStats`, matching the C4 diagram's
  **Cloud Save** container. Each peer saves **only its own** result at the end of a match
  (`MatchNetwork.RecordOwnResult`), so the host never stores other people's data. Storage
  failures are logged and swallowed — they can't block play.
- **What is stored** is aggregates only: matches played/won, rounds played, total points,
  best score, last-played timestamp (`Core/PlayerStats.cs`). No opponents, no clue text,
  nothing about other players.
- Covered by 5 tests in `Assets/Tests/PlayerStatsTests.cs` (accumulation, best-score
  monotonicity, empty-state division, JSON round trip).

**Verified (2026-07-30):** a finished match writes the record to Cloud Save. Sign-in logs
the `PlayerId` so the row can be found in the dashboard (Cloud Save → Player Data), a
successful save logs what it wrote, and every skip says why — no session, guest, or not
signed in — instead of returning in silence.

**Note for testing in the editor:** editor stats are throwaway. The profile is scoped to
the process there (see fixed item 16), so each editor run is a different account and the
totals start from zero. Only builds accumulate.

**Still not persisted (a scope decision, not a bug):** per-match history is kept in memory
for the end-of-match stats screen only. What survives between sessions is the aggregate.
If §2's *"histórico básico de partidas"* is meant to mean a browsable list of past matches,
that is a second key and a screen to show it.

Original finding:
The objectives promise *"Registrar histórico básico de partidas, pontuação final e
estatísticas simples do jogador"*, the user journey says *"O sistema salva o histórico"*,
and the C4 container diagram in §7 shows **Cloud Save**. Nothing is persisted today:
match history and stats live in memory and are lost when the match ends. Only the
nickname and colour are stored (PlayerPrefs).

### 2. Intellectual property of the published build **(doc)** — PARTLY DONE
**Done (2026-07-29):** renamed throughout — display name **"Adivinhe a Cor"**, code
identity **`ColorGuesser`** (namespaces, assemblies), Unity product + cloud project name,
menu paths, and a README with a "not affiliated" disclaimer in PT and EN.

**Also done:** colour names are no longer displayed anywhere (the "Hardest Color" stat now
shows just the board code), and `Tools > Adivinhe a Cor > Generate Board Palette` produces
an own palette in `Assets/Resources/BoardGenerated.csv` — computed in OkLCh, not sampled.

**Also done by the author (2026-07-29):** GitHub repo renamed and re-deployed, Unity Cloud
project renamed, and the scene switched to the generated palette
(`BoardView.boardDataResource = BoardGenerated`).

**Done (2026-07-29):** `Assets/Resources/BoardData.csv` (the retail colours) deleted, along
with its `.meta`. `BoardView`'s default resource name now points at `BoardGenerated`, so a
freshly added BoardView loads the own palette rather than looking for the deleted file.

**Remaining caveat (a decision, not a task):** the deleted file still exists in the repo's
**git history**. Purging it there needs a history rewrite (git filter-repo / BFG) plus a
force push. For an academic project, having it gone from HEAD, from every future build and
from the deployed game may well be proportionate — but it is worth knowing it is not
erased from the past.

Original finding:
§1 states the prototype must use *"identidade visual própria, nomenclaturas alternativas
e componentes autorais **caso venha a ser publicada além do ambiente acadêmico**"*.

Current state: the game is publicly deployed (GitHub Pages), the repo is public and named
`hues-n-cues`, the title shown is "Hues & Cues", and `Assets/Resources/BoardData.csv`
reproduces the original board's 480 colours **and their authored colour names**.

*Options:* rename + own palette/colour names for the public build, or keep the deploy
clearly academic/unlisted. Decide before the defence rather than be asked about it.

---

## Medium priority

### 4. Validation coverage is partial **(doc)** — MOSTLY DONE
§8 promises unit, functional, network, stability and usability testing.
- Unit tests — ✅ 52 tests total (incl. `PlayerStatsTests`, 5)
- **Network tests** — ✅ `Assets/Tests/NetworkSyncTests.cs` (17 tests): snapshot round-trip
  (round in progress, players/colours/scores, per-round points, match history, derived
  cue master, empty snapshot = lobby reset), command serialisation for all three command
  types, host-authority rejections, lobby roster + settings sync, colour conflicts.
- Functional — ⚠️ manual only
- Stability / disconnect — ⚠️ manual only
- **Usability testing with invited users** — ❌ still to do, and §8's acceptance criterion
  is explicitly *"um grupo de usuários conseguir criar uma sala… sem intervenção do
  desenvolvedor"*. This one needs people, not code.

### 5. Evidence section incomplete **(doc)**
§9 lists: Repositório GitHub (still TBD in the document), **Documentação da API**, prints
of the app, and the deploy link. Only the deploy exists. "Documentação da API" has no
obvious counterpart in this project — consider reinterpreting it as the C4 diagrams plus
the code documentation, and say so in the text.

### 6. Disconnect handling **(doc)** — DONE
**Done (2026-07-29):**
- A player dropping no longer ends the match. `MatchController.DropPlayer` marks them
  absent: they keep their score and stay on the scoreboard, the round stops waiting for
  their guess, and the cue-master rotation skips them. If their absence completes the
  phase (or it was their turn to clue), play advances immediately instead of stalling.
  Presence is synced to clients (`playerConnected`), and `SnapshotMatch.Guessers` filters
  the same way as the host so the "x/y confirmed" counter agrees.
- Below `MinPlayersToStart` the host still returns everyone to the lobby — there is no
  game left to play.
- Losing the connection now explains itself: `SessionBootstrap.Notice` is set from NGO's
  `DisconnectReason` (or a generic "A conexão com a sala foi perdida.") and shown on the
  menu, instead of the player silently finding themselves back at the start. A player
  rejected for a full room is told "A sala está cheia."
- Covered by 5 new tests in `MatchTests`.

**Reconnect DONE (2026-07-30)** — §8's *"reconexão"*. A player who drops can rejoin the
room and resume their seat: same score, same colour, same name on the scoreboard, same
place in the cue master rotation.

- `MatchController.RejoinPlayer(playerId)` is the inverse of `DropPlayer`: it clears the
  absent flag and nothing else, so returning cannot disturb the round in progress.
- `MatchNetwork.TryReseat` recognises them from their Authentication account id. It runs
  in `SetProfileRpc`, not `OnClientConnected`, because the account id does not exist yet at
  connect time — the host does not know *who* arrived until the client says so.
- **One account, one connection** (`DropStaleConnectionsFor`). A second connection under
  the same account drops the first. This covers reconnecting faster than the transport
  notices the drop, and the same account opening the game twice — which would otherwise
  let either act as the other, and stall the round, since guesses are keyed on that id.
- Presence already travelled on the wire (`playerConnected`), so clients see people leave
  and return without further work.
- **Guests cannot reconnect, by design.** A throwaway profile every time means there is
  nothing to recognise them by. Worth stating in the write-up: it is the honest cost of
  not storing anything about them.
- Covered by 6 new tests in `MatchTests` and 2 in `NetworkSyncTests`.

**Testing this needs 3+ players.** Below `MinPlayersToStart` the host sends everyone back
to the lobby, so with 2 players there is no match left to rejoin. In the editor the minimum
is 2, so use **three** virtual players: drop one, rejoin with the same (non-guest) profile.

**Hard limit worth recording:** if the **host** leaves, the match cannot be recovered.
Relay plus a host-authoritative design means the session dies with the host; host
migration is a different architecture. Clients get a clear message, which is the most that
design allows.

**Known gap, deliberately left:** a returning player is not held a slot. If the room filled
up while they were away they are refused as full, because capacity is enforced at connect
time — before the host knows who they are. Reserving would mean admitting first and
disconnecting after, which is worse for everyone else.

---

## Low priority

### 9. Dead offline/hotseat code path
`MatchView.StartHotseat()`, `LocalMatchSession` and `DefaultRoster` are unreachable —
nothing calls them since the menu lost its "Play Offline" button. Either wire an offline
entry back into the menu (handy for demoing without a second machine, and for recording
evidence) or delete them.

### 10. Accessibility
A colour-matching game with no colourblind affordance. §10 lists *"acessibilidade
aprimorada"* as future work, so this is consistent with the document — but the authored
colour names are already in the CSV and could be surfaced cheaply (e.g. on hover or in
the selected-colour display).

### 11. Visual polish
Prefabs still use default Unity UI sprites/colours in places (MenuHud, LobbyHud, MatchHud,
PlayerMarker, board cells). The `UI/Gradient Rounded` shader exists but is not applied
widely; `BoardView.cellPrefab` / `labelPrefab` are available for restyling the board.

---

## Not in the document — worth adding to it

These were built but are not described in the PDF. §5's "Polimento" phase covers them;
they are genuine delivered scope and should be written up:

- Per-phase **guess/clue timer** with host-configurable duration
- Lobby **ready-up** system and host-configured room settings (capacity, target score, time)
- **Player colour choice** with host-side conflict resolution (first come, first served)
- Reveal **round score panel** with an "everyone presses next" advance + timeout
- **Final scoreboard** with medal positions and tie-aware ranking
- End-of-match **stats** (best clue, hardest colour, exact guesses, duration)

---

## Fixed

### Polish pass (2026-07-23)
1. **Reconnecting client showed stale state** — `MatchNetwork.OnClientConnected` now always
   broadcasts the current state (an empty snapshot when no match is running).
2. **Anyone could restart a finished match** — restart and round-advance are host-gated
   server-side, not just hidden in the UI.
3. **"Next Round" was required to finish** — the final reveal now reads "See Final Scores".
4. **No tie handling** — a shared top score now shows "It's a tie between X & Y".

### Rule check (2026-07-28)
5. **Cue giver's scoring frame was too big** — it paid out for anything within distance 2
   (the 5×5 area). The rule is *within the scoring frame* = the 3×3 block, so pieces that
   only touch the outside of the frame now earn the cue giver nothing.
6. **3-player rule was missing** — the cue giver now scores **2 points per piece** in a
   3-player game, as the printed rules require.

### Mixed-language cleanup (2026-07-29)
10. **Mixed-language UI** — turned out the English strings were never visible:
    `statusText` and `scoreboardText` were unassigned in the prefab (`fileID: 0`) and the
    `StatusText` object had been deleted during the HUD rebuild. `BuildStatus`,
    `BuildScoreboard` and `ResultText` (~84 lines) ran every refresh and threw the result
    away. All removed, along with `SetStatus`/`SetScoreboard`. The UI is now entirely
    Portuguese, from the prefab strings.
11. **No running totals during a match** (found while doing the above) — with the dead
    scoreboard gone, nothing showed cumulative scores until the match ended, which is a
    problem when the win condition is "first to 25". The reveal score card now shows
    **`+3 (12)`**: points won this round plus the running total.

### Player-count rules (2026-07-29)
12. **Minimum-player rule was inconsistent** — the capacity dropdown starts at 3 and the
    scoring rules have a 3-player special case, but the host could start with 2. There is
    now a single `MatchNetwork.MinPlayersToStart` used by the server guard, the lobby's
    Start button and `EveryoneReady`: **3 in builds, 2 in the editor** (`#if UNITY_EDITOR`)
    so a match can still be exercised with two virtual players.
13. **Room capacity was not enforced** — the host's 3–10 choice only drove the `x/y`
    display, so an extra player could still join. The host now disconnects anyone who
    connects beyond the chosen capacity.

### Found while building reconnect (2026-07-30)
14. **Dropping a player did nothing in a real match** — `MatchNetwork.OnClientDisconnected`
    removed the client's account id from `_authIds` *before* calling `PlayerIdFor(clientId)`
    to identify them, so `PlayerIdFor` fell back to the client id and `DropPlayer` was
    handed an id no player had. It returned false silently. The whole of #6's host-side
    handling was therefore inert wherever the account id had arrived — which is every real
    session. Only the Core-level tests covered `DropPlayer`, so nothing caught it. The id is
    now resolved before anything is forgotten.
15. **The cue master could change mid-round, losing a guesser's points** — `CueMaster` was
    computed on demand from the round number and who was present, so dropping the cue master
    mid-round promoted a *guesser* into the role. That guesser was then excluded from
    `Guessers`, which both ended the phase early (the count it was waiting for shrank) and
    threw away the cubes they had already locked in, while crediting them with cue-master
    points for guesses they had made themselves. The player who actually gave the clues got
    nothing. The cue master is now chosen once in `BeginRound` and fixed for the round;
    the rotation still steps past absent players when the next round starts.

### Identity and session cleanup (2026-07-30)
16. **Every editor instance signed in as the same player** — a regression from the
    persistence work. The stable Authentication profile was stored in `PlayerPrefs`, but
    Multiplayer Play Mode virtual players **share PlayerPrefs** with the main editor (one
    registry key per company/product, same project), so every instance read the same
    profile and signed into the same anonymous account. Joining a room then failed with
    "player is already in the session", because it was. `StableProfile()` is now scoped to
    the process in the editor — distinct per virtual player, stable across play mode so
    reconnection can still be tested — and keeps the stored profile in builds. The same
    collision exists between **two tabs of one browser**, which share `localStorage`; use
    two browsers or a private window when testing a build.
17. **Losing the connection left us a member of the room** — `OnClientDisconnect` cleared
    the local session reference without ever calling `LeaveAsync`, so after any involuntary
    exit (host closed the room, connection dropped, rejected as full) the service still
    listed the player as present and the next join was refused. `Leave()` also swallowed a
    failed `LeaveAsync` while reporting success, and never set `_busy`, so a Join click
    during the await hit the `InSession` guard and did nothing. All paths now go through
    `ReleaseSessionAsync`, which tells the service first, then clears local state and shuts
    the transport down as a backstop.

### UI reuse pass (2026-07-28)
7. **Board stayed visible on the final screen** — gameplay and final screens are now
   mutually exclusive (`gameplayRoot` / `finalScreenRoot`).
8. **Panels were not reset between matches** — `ResetForNewMatch()` clears every panel and
   this client's per-match state; `StartMatchServer` clears leftover vote/timer state.
9. **Stale round scores on the wire** — `_roundScores` is now cleared in `BeginRound`, so
   mid-round snapshots no longer carry the previous round's points.
