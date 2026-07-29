using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// Shows a single colour with its board code (e.g. J17). Shared by both roles:
    ///   - the cue master sees the secret colour of the round,
    ///   - a guesser sees the cell they picked, before confirming it,
    ///   - at the reveal everyone sees the target.
    /// Living outside the role panels is what lets it be reused for all three.
    /// </summary>
    public class ColorDisplayView : MonoBehaviour
    {
        [Tooltip("Image tinted with the colour.")]
        [SerializeField] private Image colorImage;

        [Tooltip("The cell's code, e.g. J17.")]
        [SerializeField] private TextMeshProUGUI codeText;

        [Tooltip("Optional: the colour's authored name, e.g. \"robin's egg blue\".")]
        [SerializeField] private TextMeshProUGUI nameText;

        [Tooltip("Optional container hidden when there is nothing to show. Empty = this object.")]
        [SerializeField] private GameObject root;

        [SerializeField] private string emptyCodeLabel = "--";
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.15f);

        /// <summary>Shows a colour, or clears the display when hasColor is false.</summary>
        public void Show(bool hasColor, Color color, string code, string colorName = null)
        {
            var target = root != null ? root : gameObject;
            if (target.activeSelf != hasColor) target.SetActive(hasColor);
            if (!hasColor) return;

            if (colorImage != null) colorImage.color = color;
            if (codeText != null) codeText.text = string.IsNullOrEmpty(code) ? emptyCodeLabel : code;
            if (nameText != null) nameText.text = colorName ?? string.Empty;
        }

        /// <summary>Clears the display (keeps the object visible but blank, if it has no root).</summary>
        public void Clear()
        {
            if (root == null)
            {
                if (colorImage != null) colorImage.color = emptyColor;
                if (codeText != null) codeText.text = emptyCodeLabel;
                if (nameText != null) nameText.text = string.Empty;
                return;
            }
            if (root.activeSelf) root.SetActive(false);
        }
    }
}
