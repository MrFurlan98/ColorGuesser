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

            Assert.AreNotEqual(MatchPhase.CueMasterClue1, m.Phase,
                "their clue is never coming, so the phase moves on");
            Assert.AreEqual(cue.Id, m.CueMaster.Id,
                "but the seat stays theirs for this round - reassigning it mid-round would " +
                "turn a guesser into the cue master and lose their cubes");
            Assert.IsFalse(m.Guessers.Contains(cue), "they are still not a guesser");
        }

        [Test]
        public void TheCueMasterIsFixedForTheRoundSoGuessersKeepTheirPoints()
        {
            // The bug this pins down: with the cue master computed on demand, dropping them
            // mid-round promoted a guesser into the role, which both ended the phase early
            // and threw away the cubes that guesser had already locked in.
            var m = NewMatch();
            m.StartMatch();
            var cue = m.CueMaster;
            var guessers = m.Guessers.ToList();

            m.SubmitClue(cue.Id, "a");
            foreach (var g in guessers) m.SubmitGuess(g.Id, m.Target);
            m.SubmitClue(cue.Id, "b");
            m.SubmitGuess(guessers[0].Id, m.Target);   // one guesser is in

            m.DropPlayer(cue.Id);

            Assert.AreEqual(MatchPhase.Guessing2, m.Phase,
                "the round still owes the other guesser their turn");
            Assert.AreEqual(cue.Id, m.CueMaster.Id);

            m.SubmitGuess(guessers[1].Id, m.Target);
            Assert.AreEqual(MatchPhase.Reveal, m.Phase);

            Assert.AreEqual(6, guessers[0].Score, "two exact cubes still score, cue master or not");
            Assert.AreEqual(6, guessers[1].Score);
            Assert.AreEqual(8, cue.Score, "the clue was theirs, so the cubes it earned are too");
        }

        // ----- Players coming back --------------------------------------------------

        [Test]
        public void ARejoiningPlayerGetsTheirSeatAndScoreBack()
        {
            var m = NewMatch();
            m.StartMatch();
            PlayPerfectRound(m);              // everyone scores
            m.NextRound();

            var victim = m.Guessers.First();
            int scoreBefore = victim.Score;
            Assert.IsTrue(m.DropPlayer(victim.Id));
            Assert.AreEqual(2, m.ConnectedCount);

            Assert.IsTrue(m.RejoinPlayer(victim.Id));

            Assert.IsTrue(victim.IsConnected);
            Assert.AreEqual(scoreBefore, victim.Score, "they come back to the score they left with");
            Assert.IsTrue(m.Guessers.Contains(victim), "and the round waits for them again");
            Assert.AreEqual(3, m.ConnectedCount);
        }

        [Test]
        public void RejoiningDoesNotTakeTheCueMasterRoleBack()
        {
            var m = NewMatch();
            m.StartMatch();
            var cue = m.CueMaster;

            m.DropPlayer(cue.Id);             // phase moves on, seat stays theirs
            m.RejoinPlayer(cue.Id);

            Assert.AreEqual(cue.Id, m.CueMaster.Id, "they never lost the seat, so nothing changes");
            Assert.IsFalse(m.Guessers.Contains(cue));
        }

        [Test]
        public void RejoiningMidRoundMakesTheRoundWaitForThatGuessAgain()
        {
            var m = NewMatch();
            m.StartMatch();
            m.SubmitClue(m.CueMaster.Id, "quente");
            var guessers = m.Guessers.ToList();

            // One guesser answers, the other leaves - which completes the phase.
            m.SubmitGuess(guessers[0].Id, m.Target);
            m.DropPlayer(guessers[1].Id);
            Assert.AreEqual(MatchPhase.CueMasterClue2, m.Phase);

            // They come back for the second guessing phase and are owed their turn.
            m.RejoinPlayer(guessers[1].Id);
            m.SubmitClue(m.CueMaster.Id, "vermelho");
            Assert.AreEqual(MatchPhase.Guessing2, m.Phase);

            m.SubmitGuess(guessers[0].Id, m.Target);
            Assert.AreEqual(MatchPhase.Guessing2, m.Phase, "still waiting for the player who returned");

            Assert.IsTrue(m.SubmitGuess(guessers[1].Id, m.Target), "and they are allowed to guess");
            Assert.AreEqual(MatchPhase.Reveal, m.Phase);
        }

        [Test]
        public void TheRotationSkipsAnAbsentPlayerAndTakesThemBackOnceTheyReturn()
        {
            var m = NewMatch(targetScore: 500); // high, so the match cannot end mid-loop
            m.StartMatch();

            // Second in the list, so the rotation would hand them round 2 - which makes
            // the skip observable rather than something that was going to happen anyway.
            var absent = m.Players[1];
            m.DropPlayer(absent.Id);

            PlayPerfectRound(m); m.NextRound();                       // -> round 2
            Assert.AreNotEqual(absent.Id, m.CueMaster.Id, "round 2 was theirs; it must skip them");

            m.RejoinPlayer(absent.Id);

            // The turn rotates by round number, so theirs comes round again at round 5.
            PlayPerfectRound(m); m.NextRound();                       // -> round 3
            PlayPerfectRound(m); m.NextRound();                       // -> round 4
            PlayPerfectRound(m); m.NextRound();                       // -> round 5
            Assert.AreEqual(absent.Id, m.CueMaster.Id, "back in the rotation");
        }

        [Test]
        public void RejoiningAnUnknownOrPresentPlayerDoesNothing()
        {
            var m = NewMatch();
            m.StartMatch();
            var victim = m.Guessers.First();

            Assert.IsFalse(m.RejoinPlayer(victim.Id), "they never left");
            Assert.IsFalse(m.RejoinPlayer("nobody"));

            m.DropPlayer(victim.Id);
            Assert.IsTrue(m.RejoinPlayer(victim.Id));
            Assert.IsFalse(m.RejoinPlayer(victim.Id), "rejoining twice is a no-op");
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
