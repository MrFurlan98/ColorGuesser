using System.Runtime.InteropServices;
using UnityEngine;

namespace ColorGuesser.Game
{
    /// <summary>
    /// Copying text to the system clipboard, on the browser as well as everywhere else.
    ///
    /// Unity's GUIUtility.systemCopyBuffer silently does nothing in a WebGL build - the
    /// clipboard is the browser's, and only JavaScript may write to it - so the room code
    /// looked copied and was not. On WebGL this goes through a small .jslib bridge
    /// (Assets/Plugins/WebGL/ClipboardPlugin.jslib); everywhere else the built-in works.
    /// </summary>
    public static class Clipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ColorGuesserCopyToClipboard(string text);
#endif

        /// <summary>Puts the text on the clipboard. Empty text is ignored.</summary>
        public static void Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            ColorGuesserCopyToClipboard(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }
    }
}
