using UnityEngine;

namespace HuesNCues.Core
{
    /// <summary>
    /// The 10 colors a player can pick for their marker. Colors are referenced by
    /// INDEX everywhere (menu, network, markers) so only a small int travels over the
    /// wire and every client resolves the same color.
    ///
    /// The order here must match the order of the colour toggles in the MenuHud prefab.
    /// </summary>
    public static class PlayerPalette
    {
        public static readonly Color[] Colors =
        {
            new Color(0.90f, 0.25f, 0.25f), // 0 red
            new Color(0.95f, 0.55f, 0.15f), // 1 orange
            new Color(0.95f, 0.85f, 0.20f), // 2 yellow
            new Color(0.45f, 0.78f, 0.28f), // 3 green
            new Color(0.15f, 0.65f, 0.55f), // 4 teal
            new Color(0.25f, 0.55f, 0.90f), // 5 blue
            new Color(0.35f, 0.32f, 0.75f), // 6 indigo
            new Color(0.65f, 0.35f, 0.80f), // 7 purple
            new Color(0.95f, 0.45f, 0.70f), // 8 pink
            new Color(0.55f, 0.40f, 0.30f), // 9 brown
        };

        public static int Count => Colors.Length;

        /// <summary>Safe lookup: any out-of-range index falls back to white.</summary>
        public static Color Get(int index) =>
            (index >= 0 && index < Colors.Length) ? Colors[index] : Color.white;

        /// <summary>Clamps an arbitrary value into a valid palette index.</summary>
        public static int Clamp(int index) => Mathf.Clamp(index, 0, Colors.Length - 1);

        /// <summary>Hex (RRGGBB) for TextMeshPro rich text, e.g. &lt;color=#FF0000&gt;.</summary>
        public static string Hex(int index) => ColorUtility.ToHtmlStringRGB(Get(index));
    }
}
