using System.Collections.Generic;

namespace HuesNCues.Core
{
    /// <summary>
    /// The temporary fixed player list, used until the lobby/nickname screen exists.
    /// Shared by the offline path and the network host so both build the same players
    /// in the same order (order matters for consistent player colors across clients).
    /// </summary>
    public static class DefaultRoster
    {
        public static List<Player> Create() => new List<Player>
        {
            new Player("p1", "Ana"),
            new Player("p2", "Bia"),
            new Player("p3", "Caio"),
        };
    }
}
