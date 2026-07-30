using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorGuesser.Core
{
    /// <summary>
    /// The color grid: 30 columns x 16 rows = 480 cells, matching the proposal.
    ///
    /// The board is now DATA-DRIVEN: each cell's color and name come from the
    /// authored spreadsheet (see BoardCsvParser + Assets/Resources/BoardData.csv),
    /// not from a formula. The colors are still fixed, so every client that loads
    /// the same file shows the same board and the host only needs to send the
    /// secret coordinate over the network.
    /// </summary>
    public class ColorBoard
    {
        public const int Columns = 30;
        public const int Rows = 16;

        public int CellCount => Columns * Rows;

        private readonly Color[] _colors; // indexed by Index(coord)
        private readonly string[] _names;

        public ColorBoard(Color[] colors, string[] names)
        {
            if (colors == null || colors.Length != Columns * Rows)
                throw new ArgumentException($"Expected {Columns * Rows} colors, got {colors?.Length}.");
            if (names == null || names.Length != Columns * Rows)
                throw new ArgumentException($"Expected {Columns * Rows} names, got {names?.Length}.");
            _colors = colors;
            _names = names;
        }

        /// <summary>True if the coordinate is inside the board bounds.</summary>
        public bool Contains(GridCoordinate c)
            => c.Column >= 0 && c.Column < Columns && c.Row >= 0 && c.Row < Rows;

        /// <summary>The authored color at a given cell.</summary>
        public Color GetColor(GridCoordinate c) => _colors[Index(c)];

        /// <summary>The authored human name of a given cell, e.g. "robin's egg blue".</summary>
        public string GetName(GridCoordinate c) => _names[Index(c)];

        /// <summary>Every coordinate on the board, row by row (top-left first).</summary>
        public IEnumerable<GridCoordinate> AllCoordinates()
        {
            for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Columns; col++)
                    yield return new GridCoordinate(col, row);
        }

        internal static int Index(GridCoordinate c) => c.Row * Columns + c.Column;

        /// <summary>
        /// A procedurally generated fallback board, used if the authored data file
        /// is missing and handy in tests. Column drives the hue; the row goes from a
        /// light tint at the top through the pure hue to a dark shade at the bottom.
        /// </summary>
        public static ColorBoard CreateProcedural()
        {
            var colors = new Color[Columns * Rows];
            var names = new string[Columns * Rows];
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    var coord = new GridCoordinate(col, row);
                    float hue = (float)col / Columns;
                    float t = (float)row / (Rows - 1);

                    float sat, val;
                    if (t < 0.5f) { sat = Mathf.Lerp(0.15f, 1f, t / 0.5f); val = 1f; }
                    else { sat = 1f; val = Mathf.Lerp(1f, 0.25f, (t - 0.5f) / 0.5f); }

                    int i = Index(coord);
                    colors[i] = Color.HSVToRGB(hue, sat, val);
                    names[i] = coord.Label;
                }
            }
            return new ColorBoard(colors, names);
        }
    }
}
