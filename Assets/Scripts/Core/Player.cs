namespace ColorGuesser.Core
{
    /// <summary>
    /// A participant in a match. Score accumulates across rounds. Only the Core
    /// assembly (and the Net assembly, which rebuilds state from the host) can change
    /// the score or colour, so the UI can read them but never tamper with them.
    /// </summary>
    public class Player
    {
        public string Id { get; }
        public string Name { get; }
        public int Score { get; internal set; }

        /// <summary>Index into <see cref="PlayerPalette"/>: this player's marker colour.
        /// Chosen in the menu; the host guarantees it is unique per match.</summary>
        public int ColorIndex { get; internal set; }

        /// <summary>
        /// False once the player has dropped out. They keep their score and stay on the
        /// scoreboard, but the round no longer waits for them and they are skipped when
        /// the cue master rotates.
        /// </summary>
        public bool IsConnected { get; internal set; } = true;

        public Player(string id, string name, int colorIndex = 0)
        {
            Id = id;
            Name = name;
            ColorIndex = colorIndex;
        }
    }
}
