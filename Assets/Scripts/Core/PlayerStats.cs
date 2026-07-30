using System;

namespace ColorGuesser.Core
{
    /// <summary>
    /// A player's lifetime record, kept between sessions. Plain serialisable data with the
    /// merge rule as a method, so the accumulation can be unit tested without touching the
    /// network or any storage service.
    ///
    /// Only aggregates are stored - no opponents, no clue text, nothing about other
    /// players - which keeps what is saved about a person to the minimum the stats need.
    /// </summary>
    [Serializable]
    public class PlayerStats
    {
        public int matchesPlayed;
        public int matchesWon;
        public int roundsPlayed;

        /// <summary>Points scored across every match.</summary>
        public int totalPoints;

        /// <summary>Best final score in a single match.</summary>
        public int bestScore;

        /// <summary>When the last match finished, ISO-8601 UTC. Empty if never played.</summary>
        public string lastPlayedUtc = "";

        /// <summary>Average points per match, or 0 before the first one.</summary>
        public float AveragePoints => matchesPlayed > 0 ? (float)totalPoints / matchesPlayed : 0f;

        /// <summary>Share of matches won, 0..1.</summary>
        public float WinRate => matchesPlayed > 0 ? (float)matchesWon / matchesPlayed : 0f;

        /// <summary>Folds one finished match into the record.</summary>
        public void AddMatch(int finalScore, bool won, int rounds, DateTime finishedUtc)
        {
            matchesPlayed++;
            if (won) matchesWon++;
            roundsPlayed += Math.Max(0, rounds);
            totalPoints += Math.Max(0, finalScore);
            if (finalScore > bestScore) bestScore = finalScore;
            lastPlayedUtc = finishedUtc.ToString("o");
        }
    }
}
