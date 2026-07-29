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
        /// <summary>Points needed to win. Rounds continue until somebody reaches it.</summary>
        int TargetScore { get; }
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

        /// <summary>Points each player earned in the round just revealed (not the total).</summary>
        IReadOnlyDictionary<string, int> RoundScores { get; }

        /// <summary>One entry per finished round, for the end-of-match stats.</summary>
        IReadOnlyList<RoundRecord> History { get; }

        /// <summary>How long the match has been running (frozen once it finishes).</summary>
        float ElapsedSeconds { get; }
    }
}
