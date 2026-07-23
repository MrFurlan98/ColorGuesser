using System.Collections.Generic;
using UnityEngine;

namespace HuesNCues.Core
{
    /// <summary>
    /// The color grid: 30 columns x 16 rows = 480 cells, matching the proposal.
    ///
    /// Colors are generated deterministically from the coordinate. That means every
    /// player running this same code builds the exact same board, so the host does
    /// not need to send 480 colors over the network - it only needs to send the
    /// secret coordinate, and each client can look up the color itself.
    /// </summary>
    public class ColorBoard
    {
        public const int Columns = 30;
        public const int Rows = 16;

        public int CellCount => Columns * Rows;

        /// <summary>True if the coordinate is inside the board bounds.</summary>
        public bool Contains(GridCoordinate c)
            => c.Column >= 0 && c.Column < Columns && c.Row >= 0 && c.Row < Rows;

        /// <summary>
        /// The color displayed at a given cell.
        ///
        /// - The COLUMN chooses the hue (its position around the color wheel).
        /// - The ROW goes from a light tint at the top, through the pure hue in the
        ///   middle, down to a dark shade at the bottom - just like the printed board.
        /// </summary>
        public Color GetColor(GridCoordinate c)
        {
            float hue = (float)c.Column / Columns;           // 0..1 around the wheel
            float t = (float)c.Row / (Rows - 1);             // 0 at top row, 1 at bottom row

            float saturation, value;
            if (t < 0.5f)
            {
                // Top half: near-white tint -> fully saturated pure color.
                float k = t / 0.5f;                          // 0..1
                saturation = Mathf.Lerp(0.15f, 1f, k);
                value = 1f;
            }
            else
            {
                // Bottom half: pure color -> near-black shade.
                float k = (t - 0.5f) / 0.5f;                 // 0..1
                saturation = 1f;
                value = Mathf.Lerp(1f, 0.25f, k);
            }

            return Color.HSVToRGB(hue, saturation, value);
        }

        /// <summary>Every coordinate on the board, row by row (top-left first).</summary>
        public IEnumerable<GridCoordinate> AllCoordinates()
        {
            for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Columns; col++)
                    yield return new GridCoordinate(col, row);
        }
    }
}
