namespace ColorGuesser.Net
{
    /// <summary>
    /// Room settings chosen by the host in the lobby. Travels inside the LobbyRoster
    /// so every client sees the same values, and is applied when the match starts.
    /// </summary>
    [System.Serializable]
    public class LobbySettings
    {
        /// <summary>Room capacity (3-10).</summary>
        public int maxPlayers = 6;

        /// <summary>Points needed to win the match.</summary>
        public int targetScore = 25;

        /// <summary>Seconds allowed per guessing phase; 0 means no limit.</summary>
        public int guessSeconds = 60;

        public LobbySettings Clone() => new LobbySettings
        {
            maxPlayers = maxPlayers,
            targetScore = targetScore,
            guessSeconds = guessSeconds,
        };
    }
}
