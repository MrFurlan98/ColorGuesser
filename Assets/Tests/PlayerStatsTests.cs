using System;
using ColorGuesser.Core;
using NUnit.Framework;
using UnityEngine;

namespace ColorGuesser.Tests
{
    /// <summary>
    /// The stored player record. Kept as plain data with the accumulation rule in Core, so
    /// the arithmetic can be checked without Cloud Save, a network or a scene.
    /// </summary>
    public class PlayerStatsTests
    {
        private static readonly DateTime When = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void FreshStatsAreEmptyAndDoNotDivideByZero()
        {
            var stats = new PlayerStats();

            Assert.AreEqual(0, stats.matchesPlayed);
            Assert.AreEqual(0f, stats.AveragePoints);
            Assert.AreEqual(0f, stats.WinRate);
            Assert.IsEmpty(stats.lastPlayedUtc);
        }

        [Test]
        public void AMatchIsFoldedIntoTheRecord()
        {
            var stats = new PlayerStats();
            stats.AddMatch(finalScore: 25, won: true, rounds: 6, finishedUtc: When);

            Assert.AreEqual(1, stats.matchesPlayed);
            Assert.AreEqual(1, stats.matchesWon);
            Assert.AreEqual(6, stats.roundsPlayed);
            Assert.AreEqual(25, stats.totalPoints);
            Assert.AreEqual(25, stats.bestScore);
            Assert.IsNotEmpty(stats.lastPlayedUtc);
        }

        [Test]
        public void MatchesAccumulateAndBestScoreOnlyRises()
        {
            var stats = new PlayerStats();
            stats.AddMatch(25, true, 6, When);
            stats.AddMatch(11, false, 5, When);   // a worse match must not lower the best
            stats.AddMatch(30, false, 7, When);

            Assert.AreEqual(3, stats.matchesPlayed);
            Assert.AreEqual(1, stats.matchesWon);
            Assert.AreEqual(18, stats.roundsPlayed);
            Assert.AreEqual(66, stats.totalPoints);
            Assert.AreEqual(30, stats.bestScore);
            Assert.AreEqual(22f, stats.AveragePoints, 0.01f);
            Assert.AreEqual(1f / 3f, stats.WinRate, 0.01f);
        }

        [Test]
        public void StatsSurviveTheJsonRoundTripUsedByCloudSave()
        {
            var stats = new PlayerStats();
            stats.AddMatch(25, true, 6, When);
            stats.AddMatch(18, false, 4, When);

            var restored = JsonUtility.FromJson<PlayerStats>(JsonUtility.ToJson(stats));

            Assert.AreEqual(stats.matchesPlayed, restored.matchesPlayed);
            Assert.AreEqual(stats.matchesWon, restored.matchesWon);
            Assert.AreEqual(stats.roundsPlayed, restored.roundsPlayed);
            Assert.AreEqual(stats.totalPoints, restored.totalPoints);
            Assert.AreEqual(stats.bestScore, restored.bestScore);
            Assert.AreEqual(stats.lastPlayedUtc, restored.lastPlayedUtc);
        }

        [Test]
        public void ZeroPointMatchesStillCount()
        {
            var stats = new PlayerStats();
            stats.AddMatch(0, false, 3, When);

            Assert.AreEqual(1, stats.matchesPlayed, "a match played is a match played");
            Assert.AreEqual(0, stats.totalPoints);
            Assert.AreEqual(0, stats.bestScore);
        }
    }
}
