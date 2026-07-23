namespace HuesNCues.Core
{
    /// <summary>
    /// A participant in a match. Score accumulates across rounds. Only the Core
    /// assembly can change the score (via the match logic), so the UI/network layers
    /// can read it but never tamper with it.
    /// </summary>
    public class Player
    {
        public string Id { get; }
        public string Name { get; }
        public int Score { get; internal set; }

        public Player(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
