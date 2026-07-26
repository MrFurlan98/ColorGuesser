using System.Runtime.CompilerServices;

// The networking layer reconstructs match state received from the host, which
// includes setting each player's score. Player.Score has an internal setter to keep
// it out of the public API, so we grant the Net assembly access to Core internals.
[assembly: InternalsVisibleTo("HuesNCues.Net")]
