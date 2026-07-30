using System;
using System.Collections.Generic;

namespace ColorGuesser.Core
{
    /// <summary>
    /// Decides which palette colour a player ends up with. Kept as a pure function so the
    /// rule can be unit tested: the host calls it, but it needs no network or scene.
    ///
    /// The rule is first come, first served - a player keeps the colour they asked for
    /// unless somebody already holds it, in which case they get a random free one.
    /// </summary>
    public static class ColorAssignment
    {
        private static readonly Random SharedRng = new Random();

        /// <summary>
        /// Resolves a colour request against the colours already taken by other players.
        /// Pass an rng to make the outcome repeatable in tests.
        /// </summary>
        public static int Resolve(int requested, IEnumerable<int> takenByOthers, Random rng = null)
        {
            requested = PlayerPalette.Clamp(requested);

            var taken = new HashSet<int>(takenByOthers ?? Array.Empty<int>());
            if (!taken.Contains(requested)) return requested;

            var free = new List<int>();
            for (int i = 0; i < PlayerPalette.Count; i++)
                if (!taken.Contains(i)) free.Add(i);

            // More players than colours: allow the repeat rather than fail.
            if (free.Count == 0) return requested;

            return free[(rng ?? SharedRng).Next(free.Count)];
        }
    }
}
