using System.Collections.Generic;

namespace ColorGuesser.Core
{
    /// <summary>
    /// The fixed player list used by the offline hotseat game (online matches build
    /// their roster from the connected clients instead). Each player gets a distinct
    /// <see cref="PlayerPalette"/> colour.
    /// </summary>
    public static class DefaultRoster
    {
        public static List<Player> Create() => new List<Player>
        {
            new Player("p1", "Ana",  0), // red
            new Player("p2", "Bia",  5), // blue
            new Player("p3", "Caio", 3), // green
        };
    }
}
