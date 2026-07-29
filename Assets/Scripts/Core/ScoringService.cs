using System.Collections.Generic;

namespace HuesNCues.Core
{
    /// <summary>
    /// The scoring rules, as pure math: no Unity objects, no network. Because it
    /// depends on nothing else, the host can trust it as the single source of truth
    /// for points, and we can verify it with fast unit tests (see the Tests folder).
    ///
    /// The "scoring frame" is the 3x3 block centred on the target colour: the target
    /// itself plus the eight cells touching it.
    /// </summary>
    public static class ScoringService
    {
        /// <summary>
        /// How far the scoring frame reaches from the target. 1 = the 3x3 block around
        /// it (ring distance, so diagonals count the same as straight steps).
        /// </summary>
        public const int ScoringFrameRadius = 1;

        /// <summary>Player count at which the cue giver's points are doubled.</summary>
        public const int SmallGamePlayerCount = 3;

        /// <summary>
        /// Points a guesser earns:
        ///   the exact colour                          -> 3
        ///   inside the scoring frame, but not exact   -> 2
        ///   touching the outside of the frame         -> 1
        ///   anything further away                     -> 0
        /// </summary>
        public static int PointsForGuess(GridCoordinate target, GridCoordinate guess)
        {
            int d = target.DistanceTo(guess);
            if (d == 0) return 3;
            if (d <= ScoringFrameRadius) return 2;
            if (d == ScoringFrameRadius + 1) return 1;
            return 0;
        }

        /// <summary>
        /// The cue giver earns one point for every piece inside the scoring frame - and
        /// two points per piece in a 3 player game, where there are fewer pieces to
        /// collect. Pieces that only touch the outside of the frame earn them nothing.
        /// </summary>
        public static int PointsForCueGiver(GridCoordinate target, IEnumerable<GridCoordinate> guesses,
            int playerCount)
        {
            int perPiece = playerCount == SmallGamePlayerCount ? 2 : 1;

            int inFrame = 0;
            foreach (var guess in guesses)
                if (target.DistanceTo(guess) <= ScoringFrameRadius)
                    inFrame++;

            return inFrame * perPiece;
        }
    }
}
