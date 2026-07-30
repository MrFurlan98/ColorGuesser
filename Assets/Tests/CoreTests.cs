using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using ColorGuesser.Core;

namespace ColorGuesser.Tests
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
            var board = ColorBoard.CreateProcedural();
            Assert.AreEqual(480, board.CellCount);
        }

        [Test]
        public void ParserReadsColorsAndNamesFromCsv()
        {
            // Build a synthetic-but-valid board CSV: every cell is #0A141E, and each
            // cell's name encodes its own row and column so we can check the mapping.
            var sb = new StringBuilder();
            sb.Append(',').Append(string.Join(",", Enumerable.Range(1, 30))).Append(",\n");
            for (int r = 0; r < ColorBoard.Rows; r++)
            {
                char letter = (char)('A' + r);
                sb.Append(letter);
                for (int c = 0; c < ColorBoard.Columns; c++) sb.Append(",#0A141E");
                sb.Append(',').Append(letter).Append('\n');          // color line

                for (int c = 0; c < ColorBoard.Columns; c++) sb.Append(",n").Append(r).Append('_').Append(c);
                sb.Append('\n');                                     // name line
            }

            var board = BoardCsvParser.Parse(sb.ToString());

            Assert.AreEqual(480, board.CellCount);
            ColorUtility.TryParseHtmlString("#0A141E", out Color expected);
            Assert.AreEqual(expected, board.GetColor(new GridCoordinate(0, 0)));
            Assert.AreEqual("n0_0", board.GetName(new GridCoordinate(0, 0)));   // A1
            Assert.AreEqual("n15_29", board.GetName(new GridCoordinate(29, 15))); // P30
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
        public void CueGiverScoresOncePerPieceInsideTheScoringFrame()
        {
            var target = new GridCoordinate(5, 5);
            var guesses = new[]
            {
                new GridCoordinate(5, 5), // the exact colour  -> inside the frame
                new GridCoordinate(6, 6), // distance 1        -> inside the frame
                new GridCoordinate(7, 5), // distance 2        -> only touches the frame
                new GridCoordinate(9, 5), // distance 4        -> nowhere near
            };
            // 4 players: one point per piece inside the frame.
            Assert.AreEqual(2, ScoringService.PointsForCueGiver(target, guesses, playerCount: 4));
        }

        [Test]
        public void CueGiverScoresDoubleInAThreePlayerGame()
        {
            var target = new GridCoordinate(5, 5);
            var guesses = new[]
            {
                new GridCoordinate(5, 5), // inside the frame
                new GridCoordinate(4, 4), // inside the frame
                new GridCoordinate(8, 5), // outside it
            };
            Assert.AreEqual(4, ScoringService.PointsForCueGiver(target, guesses, playerCount: 3));
        }
    }
}
