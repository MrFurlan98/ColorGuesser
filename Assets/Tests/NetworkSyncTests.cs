using System.Linq;
using ColorGuesser.Core;
using ColorGuesser.Net;
using NUnit.Framework;

namespace ColorGuesser.Tests
{
    /// <summary>
    /// Tests the networking layer's data path without needing a live connection: the host
    /// captures its match state, serialises it, and a client rebuilds it. If these pass,
    /// what every player sees is the same as what the host decided - which is the whole
    /// point of the host-authoritative design.
    ///
    /// The transport itself (Relay, RPCs) is exercised by hand with two clients; what is
    /// worth automating is the serialisation, because a single missing field there causes
    /// a silent desync that is very hard to spot while playing.
    /// </summary>
    public class NetworkSyncTests
    {
        private static MatchController NewMatch(int targetScore = 25)
        {
            var players = new[]
            {
                new Player("10", "Ana", 0),
                new Player("11", "Bia", 5),
                new Player("12", "Caio", 3),
            };
            return new MatchController(players, ColorBoard.CreateProcedural(), targetScore,
                new System.Random(7));
        }

        /// <summary>
        /// Plays a full round with every guess exactly on target, so there is a reveal
        /// with non-zero scores and a history entry to synchronise.
        /// </summary>
        private static void PlayRound(MatchController m)
        {
            var cue = m.CueMaster;
            m.SubmitClue(cue.Id, "quente");
            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);
            m.SubmitClue(cue.Id, "fogo");
            foreach (var g in m.Guessers.ToList()) m.SubmitGuess(g.Id, m.Target);
        }

        /// <summary>Rebuilds a client-side view exactly as the network layer would.</summary>
        private static SnapshotMatch RoundTrip(IReadOnlyMatch host)
        {
            byte[] wire = MatchSnapshot.Capture(host).ToBytes();
            var client = new SnapshotMatch();
            client.Apply(MatchSnapshot.FromBytes(wire));
            return client;
        }

        // ----- Snapshot fidelity ----------------------------------------------------

        [Test]
        public void SnapshotCarriesTheRoundInProgress()
        {
            var host = NewMatch();
            host.StartMatch();
            host.SubmitClue(host.CueMaster.Id, "quente");
            var guesser = host.Guessers.First();
            host.SubmitGuess(guesser.Id, host.Target);

            var client = RoundTrip(host);

            Assert.AreEqual(host.Phase, client.Phase);
            Assert.AreEqual(host.RoundNumber, client.RoundNumber);
            Assert.AreEqual(host.TargetScore, client.TargetScore);
            Assert.AreEqual(host.Target, client.Target);
            Assert.AreEqual("quente", client.Clue1);
            Assert.AreEqual(host.CueMaster.Id, client.CueMaster.Id);
            Assert.AreEqual(host.Players.Count, client.Players.Count);

            // The guess that was locked in must arrive with the right coordinate.
            Assert.IsTrue(client.FirstGuesses.ContainsKey(guesser.Id));
            Assert.AreEqual(host.FirstGuesses[guesser.Id], client.FirstGuesses[guesser.Id]);
        }

        [Test]
        public void SnapshotCarriesPlayersWithColoursAndScores()
        {
            var host = NewMatch();
            host.StartMatch();
            PlayRound(host);

            var client = RoundTrip(host);

            for (int i = 0; i < host.Players.Count; i++)
            {
                Assert.AreEqual(host.Players[i].Id, client.Players[i].Id);
                Assert.AreEqual(host.Players[i].Name, client.Players[i].Name);
                Assert.AreEqual(host.Players[i].ColorIndex, client.Players[i].ColorIndex,
                    "player colours must survive the wire, or markers differ per client");
                Assert.AreEqual(host.Players[i].Score, client.Players[i].Score);
            }
        }

        [Test]
        public void SnapshotCarriesThisRoundsPointsSeparatelyFromTheTotals()
        {
            var host = NewMatch();
            host.StartMatch();
            PlayRound(host);          // everyone exact: guessers score 6 each

            var client = RoundTrip(host);

            foreach (var player in host.Players)
            {
                Assert.AreEqual(host.RoundScores[player.Id], client.RoundScores[player.Id],
                    "the reveal panel shows round points, so they must sync separately");
                Assert.AreNotEqual(0, client.RoundScores[player.Id]);
            }
        }

        [Test]
        public void SnapshotCarriesTheHistoryUsedByTheEndOfMatchStats()
        {
            var host = NewMatch();
            host.StartMatch();
            PlayRound(host);
            host.NextRound();
            PlayRound(host);

            var client = RoundTrip(host);

            Assert.AreEqual(host.History.Count, client.History.Count);
            for (int i = 0; i < host.History.Count; i++)
            {
                Assert.AreEqual(host.History[i].RoundNumber, client.History[i].RoundNumber);
                Assert.AreEqual(host.History[i].Clue1, client.History[i].Clue1);
                Assert.AreEqual(host.History[i].Clue2, client.History[i].Clue2);
                Assert.AreEqual(host.History[i].Target, client.History[i].Target);
                Assert.AreEqual(host.History[i].TotalPoints, client.History[i].TotalPoints);
                Assert.AreEqual(host.History[i].ExactGuesses, client.History[i].ExactGuesses);
            }
            Assert.AreEqual(host.ElapsedSeconds, client.ElapsedSeconds, 1f);
        }

        [Test]
        public void ClientDerivesCueMasterAndGuessersFromTheSnapshot()
        {
            // Only an index travels on the wire; the client rebuilds the roles from it.
            var host = NewMatch();
            host.StartMatch();
            var client = RoundTrip(host);

            Assert.AreEqual(host.CueMaster.Id, client.CueMaster.Id);
            CollectionAssert.AreEquivalent(
                host.Guessers.Select(p => p.Id).ToList(),
                client.Guessers.Select(p => p.Id).ToList());
        }

        [Test]
        public void EmptySnapshotPutsTheClientBackInTheLobby()
        {
            var host = NewMatch();
            host.StartMatch();
            PlayRound(host);

            var client = RoundTrip(host);
            Assert.AreEqual(MatchPhase.Reveal, client.Phase);

            // This is what the host broadcasts when a match ends or a player drops.
            client.Apply(MatchSnapshot.FromBytes(MatchSnapshot.Capture(new SnapshotMatch()).ToBytes()));

            Assert.AreEqual(MatchPhase.NotStarted, client.Phase);
            Assert.IsEmpty(client.Players);
            Assert.IsEmpty(client.FirstGuesses);
            Assert.IsNull(client.CueMaster);
        }

        // ----- Commands over the wire -----------------------------------------------

        [Test]
        public void ClueCommandSurvivesTheWire()
        {
            var sent = new SubmitClueCommand { PlayerId = "11", Word = "quente" };
            var received = CommandDto.FromBytes(CommandDto.From(sent).ToBytes()).ToCommand();

            var clue = received as SubmitClueCommand;
            Assert.IsNotNull(clue);
            Assert.AreEqual("11", clue.PlayerId);
            Assert.AreEqual("quente", clue.Word);
        }

        [Test]
        public void GuessCommandSurvivesTheWire()
        {
            var sent = new SubmitGuessCommand { PlayerId = "12", Coord = new GridCoordinate(17, 9) };
            var received = CommandDto.FromBytes(CommandDto.From(sent).ToBytes()).ToCommand();

            var guess = received as SubmitGuessCommand;
            Assert.IsNotNull(guess);
            Assert.AreEqual("12", guess.PlayerId);
            Assert.AreEqual(new GridCoordinate(17, 9), guess.Coord);
        }

        [Test]
        public void NextRoundCommandSurvivesTheWire()
        {
            var sent = new NextRoundCommand { PlayerId = "10" };
            var dto = CommandDto.From(sent);

            Assert.AreEqual(2, dto.type, "the server checks type 2 to host-gate round advances");
            var received = CommandDto.FromBytes(dto.ToBytes()).ToCommand();
            Assert.IsInstanceOf<NextRoundCommand>(received);
        }

        [Test]
        public void ACommandFromTheWireHasTheSameEffectAsARunLocally()
        {
            var direct = NewMatch();
            var viaWire = NewMatch();
            direct.StartMatch();
            viaWire.StartMatch();

            var command = new SubmitClueCommand { PlayerId = direct.CueMaster.Id, Word = "quente" };
            command.ApplyTo(direct);
            CommandDto.FromBytes(CommandDto.From(command).ToBytes()).ToCommand().ApplyTo(viaWire);

            Assert.AreEqual(direct.Phase, viaWire.Phase);
            Assert.AreEqual(direct.Clue1, viaWire.Clue1);
        }

        // ----- Host authority -------------------------------------------------------

        [Test]
        public void AGuessSentForAnotherPlayerIsRejected()
        {
            // The server compares the command's player id against the sender's client id;
            // this is the rule that check protects.
            var host = NewMatch();
            host.StartMatch();
            host.SubmitClue(host.CueMaster.Id, "quente");

            Assert.IsFalse(host.SubmitGuess(host.CueMaster.Id, host.Target),
                "the cue master must not be able to guess");
            Assert.IsFalse(host.SubmitGuess("nobody", host.Target),
                "an unknown player id must not be able to guess");
        }

        [Test]
        public void OnlyTheCueMasterCanSubmitAClue()
        {
            var host = NewMatch();
            host.StartMatch();

            Assert.IsFalse(host.SubmitClue(host.Guessers.First().Id, "nope"));
            Assert.IsTrue(host.SubmitClue(host.CueMaster.Id, "quente"));
        }

        // ----- Lobby state ----------------------------------------------------------

        [Test]
        public void LobbyRosterSurvivesTheWireIncludingSettings()
        {
            var sent = new LobbyRoster
            {
                clientIds = new long[] { 0, 3 },
                names = new[] { "Ana", "Bia" },
                colorIndexes = new[] { 2, 7 },
                ready = new[] { true, false },
                hostId = 0,
                settings = new LobbySettings { maxPlayers = 8, targetScore = 30, guessSeconds = 45 },
            };

            var received = LobbyRoster.FromBytes(sent.ToBytes());

            CollectionAssert.AreEqual(sent.clientIds, received.clientIds);
            CollectionAssert.AreEqual(sent.names, received.names);
            CollectionAssert.AreEqual(sent.colorIndexes, received.colorIndexes);
            CollectionAssert.AreEqual(sent.ready, received.ready);
            Assert.AreEqual(sent.hostId, received.hostId);
            Assert.AreEqual(8, received.settings.maxPlayers);
            Assert.AreEqual(30, received.settings.targetScore);
            Assert.AreEqual(45, received.settings.guessSeconds);
        }

        // ----- Colour conflicts -----------------------------------------------------

        [Test]
        public void AFreeColourIsGrantedAsRequested()
        {
            Assert.AreEqual(4, ColorAssignment.Resolve(4, new[] { 0, 1, 2 }));
        }

        [Test]
        public void AColourAlreadyTakenIsSwappedForAFreeOne()
        {
            int given = ColorAssignment.Resolve(2, new[] { 2 }, new System.Random(1));

            Assert.AreNotEqual(2, given, "first come, first served: the colour was taken");
            Assert.IsTrue(given >= 0 && given < PlayerPalette.Count);
        }

        [Test]
        public void WithEveryColourTakenTheRequestIsAllowedToRepeat()
        {
            var all = Enumerable.Range(0, PlayerPalette.Count).ToArray();
            Assert.AreEqual(3, ColorAssignment.Resolve(3, all),
                "more players than colours must not fail, just repeat");
        }

        [Test]
        public void AnOutOfRangeColourRequestIsClamped()
        {
            int given = ColorAssignment.Resolve(999, System.Array.Empty<int>());
            Assert.AreEqual(PlayerPalette.Count - 1, given);
        }
    }
}
