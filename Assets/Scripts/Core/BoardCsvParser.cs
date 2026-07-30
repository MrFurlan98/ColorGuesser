using System;
using UnityEngine;

namespace ColorGuesser.Core
{
    /// <summary>
    /// Turns the board spreadsheet (exported as CSV) into a ColorBoard.
    ///
    /// Expected layout, matching the Google Sheet:
    ///   - a header row of column numbers                          -> ignored
    ///   - then, for each of the 16 board rows (letters A..P):
    ///       * a "color line":  LETTER, hex, hex, ... (30 hexes), LETTER
    ///       * a "name line" :  (empty), name, name, ... (30 names)
    ///   - a trailing footer row of column numbers                 -> ignored
    ///
    /// Only string work happens here (plus Unity's hex color parser), so it needs
    /// no files, no scene and no network - and can be checked by unit tests.
    /// </summary>
    public static class BoardCsvParser
    {
        public static ColorBoard Parse(string csv)
        {
            if (string.IsNullOrEmpty(csv))
                throw new ArgumentException("Board CSV is empty.");

            var colors = new Color[ColorBoard.Columns * ColorBoard.Rows];
            var names = new string[ColorBoard.Columns * ColorBoard.Rows];
            var rowSeen = new bool[ColorBoard.Rows];

            string[] lines = csv.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string[] fields = lines[i].Split(',');
                if (fields.Length < ColorBoard.Columns + 1) continue;

                // A "color line" starts with a single row letter A..P. Header, footer
                // and name lines start with an empty cell, so they are skipped here.
                string first = fields[0].Trim();
                if (first.Length != 1) continue;
                int row = char.ToUpperInvariant(first[0]) - 'A';
                if (row < 0 || row >= ColorBoard.Rows) continue;

                // Colors come from this line (columns are at index 1..30).
                for (int col = 0; col < ColorBoard.Columns; col++)
                {
                    string hex = fields[col + 1].Trim();
                    if (!ColorUtility.TryParseHtmlString(hex, out Color c))
                        throw new FormatException($"Bad color '{hex}' at row {first}, column {col + 1}.");
                    colors[row * ColorBoard.Columns + col] = c;
                }

                // Names come from the very next line.
                string[] nameFields = (i + 1 < lines.Length) ? lines[i + 1].Split(',') : Array.Empty<string>();
                for (int col = 0; col < ColorBoard.Columns; col++)
                {
                    string name = (col + 1 < nameFields.Length) ? nameFields[col + 1].Trim() : string.Empty;
                    names[row * ColorBoard.Columns + col] = name;
                }

                rowSeen[row] = true;
            }

            for (int r = 0; r < ColorBoard.Rows; r++)
                if (!rowSeen[r])
                    throw new FormatException($"Board CSV is missing row {(char)('A' + r)}.");

            return new ColorBoard(colors, names);
        }
    }
}
