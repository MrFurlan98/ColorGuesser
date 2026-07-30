using System.Linq;
using NUnit.Framework;
using ColorGuesser.Core;

namespace ColorGuesser.Tests
{
    /// <summary>
    /// Drives the Match State Machine through complete rounds and checks the phase
    /// transitions, scoring and rule enforcement - all without a scene or network.
    /// </summary>
    public class MatchTests
    {
        private static MatchController NewMatch(int targetScore = 25)
        {
            var players = new[]
            {
                new Player("A", "Ana"),
                new Player("B", "Bia"),
                new Player("C", "Caio"),
            };
            // Fixed seed -> deterministic target, so the test is repeatable.
            return new MatchController(players, ColorBoard.CreateProcedural(), targetScore, new System.Random(1));
        }

        /// <summary>Plays one complete round with every guesser guessing exactly.</summary>
        private static void PlayPerfectRound(MatchController m)
        {
            var cue = m.CueMaster;
            m.SubmitClue(cue.Id, "a");
            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);
            m.SubmitClue(cue.Id, "b");
            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);
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
            // 4 cubes, all on the exact colour, in a 3 player game: 2 points each.
            Assert.AreEqual(8, cue.Score);
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

        // ----- Players dropping out -------------------------------------------------

        [Test]
        public void ADroppedPlayerKeepsTheirScoreButIsNoLongerAGuesser()
        {
            var m = NewMatch();
            m.StartMatch();
            PlayPerfectRound(m);              // everyone scores
            m.NextRound();

            var victim = m.Guessers.First();
            int scoreBefore = victim.Score;

            Assert.IsTrue(m.DropPlayer(victim.Id));

            Assert.AreEqual(scoreBefore, victim.Score, "a dropped player keeps their points");
            Assert.Contains(victim, m.Players.ToList(), "and stays on the scoreboard");
            Assert.IsFalse(m.Guessers.Contains(victim), "but the round no longer waits for them");
            Assert.AreEqual(2, m.ConnectedCount);
        }

        [Test]
        public void DroppingTheLastAwaitedGuesserAdvancesTheRound()
        {
            var m = NewMatch();
            m.StartMatch();
            m.SubmitClue(m.CueMaster.Id, "quente");

            // One of the two guessers has answered; the round waits for the other.
            var guessers = m.Guessers.ToList();
            m.SubmitGuess(guessers[0].Id, m.Target);
            Assert.AreEqual(MatchPhase.Guessing1, m.Phase);

            m.DropPlayer(guessers[1].Id);

            Assert.AreEqual(MatchPhase.CueMasterClue2, m.Phase,
                "with nobody left to wait for, the round must not stall");
        }

        [Test]
        public void DroppingTheCueMasterHandsTheRoundOn()
        {
            var m = NewMatch();
            m.StartMatch();
            var cue = m.CueMaster;
            Assert.AreEqual(MatchPhase.CueMasterClue1, m.Phase);

            m.DropPlayer(cue.Id);

            Assert.AreNotEqual(cue.Id, m.CueMaster.Id, "the turn skips a player who left");
            Assert.IsTrue(m.CueMaster.IsConnected);
            Assert.AreNotEqual(MatchPhase.CueMasterClue1, m.Phase,
                "their clue is never coming, so the phase moves on");
        }

        [Test]
        public void TheCueMasterRotationSkipsPlayersWhoLeft()
        {
            var m = NewMatch(targetScore: 500); // high, so the match cannot end mid-loop
            m.StartMatch();

            var absent = m.Guessers.First();
            m.DropPlayer(absent.Id);

            // Play on: no round may ever hand the turn to the absent player.
            for (int i = 0; i < 6; i++)
            {
                Assert.AreNotEqual(absent.Id, m.CueMaster.Id);
                PlayPerfectRound(m);
                if (m.Phase == MatchPhase.Reveal) m.NextRound();
            }
        }

        [Test]
        public void DroppingAnUnknownOrAlreadyGonePlayerDoesNothing()
        {
            var m = NewMatch();
            m.StartMatch();
            var victim = m.Guessers.First();

            Assert.IsTrue(m.DropPlayer(victim.Id));
            Assert.IsFalse(m.DropPlayer(victim.Id), "dropping twice is a no-op");
            Assert.IsFalse(m.DropPlayer("nobody"));
        }

        [Test]
        public void MatchFinishesOnceAPlayerReachesTheTargetScore()
        {
            // Guessers score 6 per perfect round, so a target of 6 ends it in one round.
            var m = NewMatch(targetScore: 6);
            m.StartMatch();
            PlayPerfectRound(m);

            Assert.AreEqual(MatchPhase.Reveal, m.Phase);
            Assert.IsTrue(m.HasWinner);
            Assert.IsTrue(m.NextRound());
            Assert.AreEqual(MatchPhase.Finished, m.Phase);
        }

        [Test]
        public void MatchKeepsGoingWhileNobodyHasReachedTheTarget()
        {
            // 6 points per perfect round for a guesser: after one round nobody has 25.
            var m = NewMatch(targetScore: 25);
            m.StartMatch();
            PlayPerfectRound(m);

            Assert.IsFalse(m.HasWinner);
            Assert.IsTrue(m.NextRound());
            Assert.AreEqual(MatchPhase.CueMasterClue1, m.Phase); // a new round, not Finished
            Assert.AreEqual(2, m.RoundNumber);
        }

        [Test]
        public void WinnerIsTheFirstPlayerToReachTwentyFive()
        {
            var m = NewMatch(targetScore: 25);
            m.StartMatch();

            // Play rounds until someone gets there; guard against an endless loop.
            for (int i = 0; i < 20 && m.Phase != MatchPhase.Finished; i++)
            {
                PlayPerfectRound(m);
                m.NextRound();
            }

            Assert.AreEqual(MatchPhase.Finished, m.Phase);
            Assert.IsTrue(m.Players.Any(p => p.Score >= 25));
        }
    }
}
