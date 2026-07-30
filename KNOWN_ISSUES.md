# Known Issues

Backlog for the project, grouped by priority. Items marked **(doc)** are things the
proposal PDF promises that the build does not do yet — those are the ones most likely
to be noticed when the project is presented.

---

## High priority

### 1. No persistence at all **(doc)**
The objectives promise *"Registrar histórico básico de partidas, pontuação final e
estatísticas simples do jogador"*, the user journey says *"O sistema salva o histórico"*,
and the C4 container diagram in §7 shows **Cloud Save**. Nothing is persisted today:
match history and stats live in memory and are lost when the match ends. Only the
nickname and colour are stored (PlayerPrefs).

*Options:* implement Cloud Save (package already available via UGS), or narrow the claim
in the document to local-only. Whichever is chosen, the C4 diagram and the build should
agree.

### 2. Intellectual property of the published build **(doc)** — PARTLY DONE
**Done (2026-07-29):** renamed throughout — display name **"Adivinhe a Cor"**, code
identity **`ColorGuesser`** (namespaces, assemblies), Unity product + cloud project name,
menu paths, and a README with a "not affiliated" disclaimer in PT and EN.

**Also done:** colour names are no longer displayed anywhere (the "Hardest Color" stat now
shows just the board code), and `Tools > Adivinhe a Cor > Generate Board Palette` produces
an own palette in `Assets/Resources/BoardGenerated.csv` — computed in OkLCh, not sampled.

**Still to do:**
- Rename the **GitHub repo** and re-deploy so the public URL no longer says `hues-n-cues`.
- Rename the **Unity Cloud project** in the dashboard (the local string is updated, but the
  dashboard is the authoritative source).
- **Switch the board over**: set `BoardView.boardDataResource` to `BoardGenerated`, play a
  few rounds to confirm it feels right, then delete `BoardData.csv` (the retail colours).
  Until that file is gone the exposure remains.

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
- Unit tests — ✅ 34 tests total
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

### 6. Disconnect handling is harsh; no reconnect **(doc)**
One player dropping ends the match **for everyone** (back to lobby, scores lost).
§3 lists *"queda de conexão"* and §8 lists *"reconexão"* as scenarios to handle.
Also: if the **host** leaves, clients get no graceful "host left" screen — they are just
disconnected.

### 7. Room capacity is not enforced
The host picks 3–10 in the lobby, but that value only drives the `x/y` display. The Relay
session is created with a hard cap of 10, so a 7th player can still join a 6-player room.
Enforcing it needs NGO connection approval (reject clients past the chosen capacity).

### 8. Minimum-player rule is inconsistent
The capacity dropdown starts at **3** (Hues & Cues needs 3+), and the scoring rules have a
special case for exactly 3 players (cue giver scores double). But the host can still
**start a match with 2 players**, which is outside the ruleset. Decide on the minimum and
apply it in `MatchNetwork.StartMatchServer` and in the lobby's Start button gate.

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

### UI reuse pass (2026-07-28)
7. **Board stayed visible on the final screen** — gameplay and final screens are now
   mutually exclusive (`gameplayRoot` / `finalScreenRoot`).
8. **Panels were not reset between matches** — `ResetForNewMatch()` clears every panel and
   this client's per-match state; `StartMatchServer` clears leftover vote/timer state.
9. **Stale round scores on the wire** — `_roundScores` is now cleared in `BeginRound`, so
   mid-round snapshots no longer carry the previous round's points.
