# Known Issues (to address in the polish pass)

Deferred bugs/rough edges found during multiplayer development. Each has the observed
behaviour and a likely root cause / fix direction to speed up fixing later.

## 1. Reconnecting client shows a stale game state
**Observed:** After a disconnect aborts the match (host returns to the "Start Match"
waiting phase), when a client (re)connects it shows a leftover game state instead of
"waiting".
**Likely cause:** `MatchNetwork.OnClientConnected` only broadcasts a snapshot when
`_serverMatch != null`. With no active match, the connecting client gets no snapshot,
so it keeps its last `SnapshotMatch`.
**Fix direction:** On client connect, always send the current state — broadcast an
empty/`_emptyMatch` snapshot when there is no active match (ideally targeted to the
new client via RpcParams so others aren't disturbed).

## 2. Anyone can restart a finished match
**Observed:** At "Game over", any player (not just the host) can press Play Again and
restart for everyone.
**Likely cause:** `MatchNetwork.RequestRestartRpc` has no sender authorization.
**Fix direction:** Restrict restart to the host (check the sender against the host
client id), or gate "Play Again" so only the host sees/triggers it.

## 3. "Next Round" is required to reach the Game-over screen
**Observed:** On the final round's reveal, you must click "Next Round" to actually
finish the game; the label is misleading at that point.
**Likely cause:** `MatchController.NextRound` moves from the last Reveal to `Finished`;
the button label in `MatchView.Refresh` is always "Next Round" until already Finished.
**Fix direction:** Detect the last round in Reveal (`RoundNumber == TotalRounds`) and
label the button "Finish" / "See Final Scores".

## 4. No tie handling when scores are equal
**Observed:** When the top scores are tied, a single "winner" is still shown.
**Likely cause:** `MatchView.WinnerName` uses `OrderByDescending(Score).First()`, which
picks an arbitrary player on a tie.
**Fix direction:** Detect equal top scores and show "It's a tie between X and Y" (and
in the Finished status), instead of a single winner.
