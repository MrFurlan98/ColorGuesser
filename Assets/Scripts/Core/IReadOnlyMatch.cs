using System.Collections.Generic;

namespace HuesNCues.Core
{
    /// <summary>
    /// A read-only view of the current match state. The UI renders itself from this
    /// and never mutates it - all changes go through commands sent to an IMatchSession.
    /// MatchController implements this interface.
    /// </summary>
    public interface IReadOnlyMatch
    {
        int TotalRounds { get; }
        int RoundNumber { get; }
        MatchPhase Phase { get; }
        GridCoordinate Target { get; }
        string Clue1 { get; }
        string Clue2 { get; }

        IReadOnlyList<Player> Players { get; }
        Player CueMaster { get; }
        IEnumerable<Player> Guessers { get; }

        IReadOnlyDictionary<string, GridCoordinate> FirstGuesses { get; }
        IReadOnlyDictionary<string, GridCoordinate> SecondGuesses { get; }
    }
}
