using System.Collections.Generic;

namespace HuesNCues.Core
{
    /// <summary>
    /// The scoring rules, as pure math: no Unity objects, no network. Because it
    /// depends on nothing else, the host can trust it as the single source of truth
    /// for points, and we can verify it with fast unit tests (see the Tests folder).
    ///
    /// Rules (based on Hues &amp; Cues):
    ///   - A guesser earns points by how close the guess is to the secret color.
    ///   - The cue giver earns points when other players guess near the target,
    ///     rewarding a good clue.
    /// </summary>
    public static class ScoringService
    {
        /// <summary>
        /// Points a guesser earns:
        ///   exact cell        (distance 0) -> 3
        ///   one ring away     (distance 1) -> 2
        ///   two rings away    (distance 2) -> 1
        ///   anything further               -> 0
        /// </summary>
        public static int PointsForGuess(GridCoordinate target, GridCoordinate guess)
        {
            int d = target.DistanceTo(guess);
            switch (d)
            {
                case 0: return 3;
                case 1: return 2;
                case 2: return 1;
                default: return 0;
            }
        }

        /// <summary>
        /// The cue giver earns 1 point for every guess that lands in the two scoring
        /// rings (distance 2 or closer). A clue that pulls several players near the
        /// target is worth more than one that only helps a single player.
        /// </summary>
        public static int PointsForCueGiver(GridCoordinate target, IEnumerable<GridCoordinate> guesses)
        {
            int total = 0;
            foreach (var guess in guesses)
                if (target.DistanceTo(guess) <= 2)
                    total++;
            return total;
        }
    }
}
