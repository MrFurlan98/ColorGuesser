# Known Issues

## Fixed (polish pass, 2026-07-23)

1. **Reconnecting client showed stale state** — ✅ `MatchNetwork.OnClientConnected` now
   always broadcasts the current state (an empty snapshot when no match is running), so
   (re)connecting clients land on the lobby instead of a leftover game.
2. **Anyone could restart a finished match** — ✅ `RequestRestartRpc` now checks the
   sender is the host; `MatchView` only shows Play Again to the host; NextRound commands
   (type 2) are also host-gated server-side.
3. **"Next Round" was required to finish** — ✅ on the final round's reveal the button now
   reads **"See Final Scores"**.
4. **No tie handling** — ✅ `MatchView.ResultText` shows "It's a tie between X & Y" when the
   top score is shared.

## Remaining (lower priority)

- **Host leaving** doesn't yet give clients a graceful "host left" screen (they just
  disconnect). Part of a future robustness/reconnect pass.
- A mid-match **disconnect aborts** the match rather than continuing with the remaining
  players (MatchController can't drop a player mid-round).
- **Visual polish**: prefabs still use default UI sprites/colors (MenuHud, LobbyHud,
  MatchHud, PlayerMarker) — restyle with custom sprites.
