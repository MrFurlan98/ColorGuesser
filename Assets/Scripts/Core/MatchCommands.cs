namespace HuesNCues.Core
{
    /// <summary>
    /// An action a player wants to perform, packaged as an object (Command pattern).
    /// The command carries only plain data, so the same object can be applied
    /// in-process today and serialized to the authoritative host over the network
    /// later. ApplyTo runs it against the match and reports whether it was accepted.
    /// </summary>
    public interface IMatchCommand
    {
        bool ApplyTo(MatchController match);
    }

    /// <summary>The cue master submits a clue word.</summary>
    public sealed class SubmitClueCommand : IMatchCommand
    {
        public string PlayerId;
        public string Word;

        public bool ApplyTo(MatchController match) => match.SubmitClue(PlayerId, Word);
    }

    /// <summary>A guesser locks a cell.</summary>
    public sealed class SubmitGuessCommand : IMatchCommand
    {
        public string PlayerId;
        public GridCoordinate Coord;

        public bool ApplyTo(MatchController match) => match.SubmitGuess(PlayerId, Coord);
    }

    /// <summary>Advance from the reveal to the next round (or end the match).</summary>
    public sealed class NextRoundCommand : IMatchCommand
    {
        public string PlayerId; // who requested it (a host can decide who is allowed)

        public bool ApplyTo(MatchController match) => match.NextRound();
    }
}
