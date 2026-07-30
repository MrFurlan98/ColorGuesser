using System;

namespace ColorGuesser.Core
{
    /// <summary>
    /// A single cell position on the color board.
    /// Column = X axis (0..29), Row = Y axis (0..15).
    ///
    /// It is a readonly struct (an immutable value type): copying it is cheap and
    /// two coordinates with the same Column/Row are always considered equal. That
    /// makes it safe to send over the network and to use as a dictionary key.
    /// </summary>
    public readonly struct GridCoordinate : IEquatable<GridCoordinate>
    {
        public int Column { get; }
        public int Row { get; }

        public GridCoordinate(int column, int row)
        {
            Column = column;
            Row = row;
        }

        /// <summary>
        /// The "ring" distance used by the scoring rules: how many squares you would
        /// step through moving like a chess king, where a diagonal step counts as 1.
        /// (This is the Chebyshev distance.) The target cell is ring 0, the eight
        /// cells touching it are ring 1, the next square of cells is ring 2, etc.
        /// </summary>
        public int DistanceTo(GridCoordinate other)
        {
            int dx = Math.Abs(Column - other.Column);
            int dy = Math.Abs(Row - other.Row);
            return Math.Max(dx, dy);
        }

        /// <summary>
        /// Human-friendly label like "A1" (top-left) up to "P30" (bottom-right):
        /// rows are letters A..P (16 of them), columns are numbers 1..30.
        /// </summary>
        public string Label => $"{(char)('A' + Row)}{Column + 1}";

        public bool Equals(GridCoordinate other) => Column == other.Column && Row == other.Row;
        public override bool Equals(object obj) => obj is GridCoordinate other && Equals(other);
        public override int GetHashCode() => (Column * 397) ^ Row;
        public override string ToString() => Label;
    }
}
