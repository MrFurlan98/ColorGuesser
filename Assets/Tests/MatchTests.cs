using System.Linq;
using NUnit.Framework;
using HuesNCues.Core;

namespace HuesNCues.Tests
{
    /// <summary>
    /// Drives the Match State Machine through complete rounds and checks the phase
    /// transitions, scoring and rule enforcement - all without a scene or network.
    /// </summary>
    public class MatchTests
    {
        private static MatchController NewMatch(int totalRounds = 2)
        {
            var players = new[]
            {
                new Player("A", "Ana"),
                new Player("B", "Bia"),
                new Player("C", "Caio"),
            };
            // Fixed seed -> deterministic target, so the test is repeatable.
            return new MatchController(players, ColorBoard.CreateProcedural(), totalRounds, new System.Random(1));
        }

        [Test]
        public void FullRoundTransitionsThroughEveryPhase()
        {
            var m = NewMatch();
            m.StartMatch();
            Assert.AreEqual(MatchPhase.CueMasterClue1, m.Phase);

            var cue = m.CueMaster;
            Assert.IsTrue(m.SubmitClue(cue.Id, "warm"));
            Assert.AreEqual(MatchPhase.Guessing1, m.Phase);

            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);
            Assert.AreEqual(MatchPhase.CueMasterClue2, m.Phase);

            Assert.IsTrue(m.SubmitClue(cue.Id, "red"));
            Assert.AreEqual(MatchPhase.Guessing2, m.Phase);

            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);
            Assert.AreEqual(MatchPhase.Reveal, m.Phase);
        }

        [Test]
        public void ExactGuessesScoreThreeEachAndCueMasterScoresPerCube()
        {
            var m = NewMatch();
            m.StartMatch();
            var cue = m.CueMaster;

            m.SubmitClue(cue.Id, "warm");
            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);
            m.SubmitClue(cue.Id, "red");
            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);

            // Two guessers, each with two exact guesses: 3 + 3 = 6 points each.
            foreach (var g in m.Guessers) Assert.AreEqual(6, g.Score);
            // Cue master: 4 cubes, all inside the rings, 1 point each = 4.
            Assert.AreEqual(4, cue.Score);
        }

        [Test]
        public void CueMasterRotatesEachRound()
        {
            var m = NewMatch();
            m.StartMatch();
            var firstCue = m.CueMaster;

            // Play the round to reveal, then advance.
            m.SubmitClue(firstCue.Id, "a");
            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);
            m.SubmitClue(firstCue.Id, "b");
            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);

            Assert.IsTrue(m.NextRound());
            Assert.AreEqual(MatchPhase.CueMasterClue1, m.Phase);
            Assert.AreNotEqual(firstCue.Id, m.CueMaster.Id);
        }

        [Test]
        public void CueMasterCannotGuessAndOutOfPhaseActionsAreRejected()
        {
            var m = NewMatch();
            m.StartMatch();
            var cue = m.CueMaster;

            // A guess before any clue is out of phase.
            Assert.IsFalse(m.SubmitGuess(m.Guessers.First().Id, m.Target));
            // A non-cue-master cannot submit the clue.
            Assert.IsFalse(m.SubmitClue(m.Guessers.First().Id, "nope"));

            m.SubmitClue(cue.Id, "warm");
            // The cue master is not allowed to guess.
            Assert.IsFalse(m.SubmitGuess(cue.Id, m.Target));
            // A guesser cannot lock twice in the same phase.
            var one = m.Guessers.First();
            Assert.IsTrue(m.SubmitGuess(one.Id, m.Target));
            Assert.IsFalse(m.SubmitGuess(one.Id, m.Target));
        }

        [Test]
        public void SingleRoundMatchFinishesAfterReveal()
        {
            var m = NewMatch(totalRounds: 1);
            m.StartMatch();
            var cue = m.CueMaster;

            m.SubmitClue(cue.Id, "a");
            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);
            m.SubmitClue(cue.Id, "b");
            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);

            Assert.AreEqual(MatchPhase.Reveal, m.Phase);
            Assert.IsTrue(m.NextRound());
            Assert.AreEqual(MatchPhase.Finished, m.Phase);
        }
    }
}
