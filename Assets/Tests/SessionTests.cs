using System.Linq;
using NUnit.Framework;
using HuesNCues.Core;

namespace HuesNCues.Tests
{
    /// <summary>
    /// Verifies the session seam: driving a match purely through commands sent to an
    /// IMatchSession produces the same results as calling the controller directly, and
    /// that StateChanged fires so a view could redraw.
    /// </summary>
    public class SessionTests
    {
        private static IMatchSession NewSession()
        {
            var players = new[]
            {
                new Player("A", "Ana"),
                new Player("B", "Bia"),
                new Player("C", "Caio"),
            };
            return new LocalMatchSession(players, ColorBoard.CreateProcedural(), 25, new System.Random(1));
        }

        [Test]
        public void CommandsDriveAFullRoundAndScore()
        {
            var s = NewSession();
            int changes = 0;
            s.StateChanged += () => changes++;

            s.Start();
            Assert.AreEqual(MatchPhase.CueMasterClue1, s.Match.Phase);

            var cue = s.Match.CueMaster;
            s.Send(new SubmitClueCommand { PlayerId = cue.Id, Word = "warm" });
            foreach (var g in s.Match.Guessers.ToList())
                s.Send(new SubmitGuessCommand { PlayerId = g.Id, Coord = s.Match.Target });

            s.Send(new SubmitClueCommand { PlayerId = cue.Id, Word = "red" });
            foreach (var g in s.Match.Guessers.ToList())
                s.Send(new SubmitGuessCommand { PlayerId = g.Id, Coord = s.Match.Target });

            Assert.AreEqual(MatchPhase.Reveal, s.Match.Phase);
            foreach (var g in s.Match.Guessers) Assert.AreEqual(6, g.Score); // two exact guesses each
            Assert.AreEqual(4, cue.Score);                                   // four cubes in the rings
            Assert.Greater(changes, 0);                                      // the view would have redrawn
        }

        [Test]
        public void RejectedCommandsDoNotChangeState()
        {
            var s = NewSession();
            s.Start();

            // A guess before any clue is out of phase and must be ignored.
            var someGuesser = s.Match.Guessers.First();
            s.Send(new SubmitGuessCommand { PlayerId = someGuesser.Id, Coord = s.Match.Target });

            Assert.AreEqual(MatchPhase.CueMasterClue1, s.Match.Phase);
            Assert.AreEqual(0, s.Match.FirstGuesses.Count);
        }
    }
}
