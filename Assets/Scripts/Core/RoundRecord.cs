namespace ColorGuesser.Core
{
    /// <summary>
    /// What happened in one finished round. The match keeps these so the end-of-game
    /// stats (best clue, hardest colour, exact guesses) can be worked out afterwards.
    /// </summary>
    public class RoundRecord
    {
        public int RoundNumber;
        public string Clue1;
        public string Clue2;
        public GridCoordinate Target;

        /// <summary>Every point scored by everyone in this round.</summary>
        public int TotalPoints;

        /// <summary>Guesses that landed exactly on the target colour.</summary>
        public int ExactGuesses;

        /// <summary>The clue words as one string, e.g. "quente fogo".</summary>
        public string ClueText =>
            string.IsNullOrWhiteSpace(Clue2) ? (Clue1 ?? "") : $"{Clue1} {Clue2}";
    }
}
