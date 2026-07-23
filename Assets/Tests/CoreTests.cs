using NUnit.Framework;
using HuesNCues.Core;

namespace HuesNCues.Tests
{
    /// <summary>
    /// Fast, deterministic checks on the game brain. These run in Unity's
    /// Test Runner (Window > General > Test Runner > EditMode) and need no
    /// scene, no UI and no network - exactly the point of keeping the core pure.
    /// </summary>
    public class CoreTests
    {
        [Test]
        public void BoardHas480Cells()
        {
            var board = new ColorBoard();
            Assert.AreEqual(480, board.CellCount);
        }

        [Test]
        public void ExactGuessScoresThree()
        {
            var target = new GridCoordinate(10, 5);
            Assert.AreEqual(3, ScoringService.PointsForGuess(target, target));
        }

        [Test]
        public void OneRingAwayScoresTwo()
        {
            var target = new GridCoordinate(10, 5);
            var guess = new GridCoordinate(11, 6); // diagonal step = distance 1
            Assert.AreEqual(2, ScoringService.PointsForGuess(target, guess));
        }

        [Test]
        public void TwoRingsAwayScoresOne()
        {
            var target = new GridCoordinate(10, 5);
            var guess = new GridCoordinate(12, 5); // two columns away = distance 2
            Assert.AreEqual(1, ScoringService.PointsForGuess(target, guess));
        }

        [Test]
        public void FarGuessScoresZero()
        {
            var target = new GridCoordinate(0, 0);
            var guess = new GridCoordinate(10, 10);
            Assert.AreEqual(0, ScoringService.PointsForGuess(target, guess));
        }

        [Test]
        public void CueGiverScoresOncePerGuessInsideTheRings()
        {
            var target = new GridCoordinate(5, 5);
            var guesses = new[]
            {
                new GridCoordinate(5, 5), // distance 0 -> counts
                new GridCoordinate(7, 5), // distance 2 -> counts
                new GridCoordinate(9, 5), // distance 4 -> does not count
            };
            Assert.AreEqual(2, ScoringService.PointsForCueGiver(target, guesses));
        }
    }
}
