using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// One cell of the colour board. Lives on the BoardCell prefab so the cell's look
    /// (sprite, border, highlight) can be authored in the editor instead of in code.
    ///
    /// Keep this prefab light: it is instantiated 480 times, so every extra graphic is
    /// 480 more UI elements. One Image plus an optional highlight that is off by
    /// default is the sweet spot.
    /// </summary>
    public class BoardCellView : MonoBehaviour
    {
        [Tooltip("The image tinted with the cell's colour. Auto-filled from this object.")]
        [SerializeField] private Image colorImage;

        [Tooltip("Optional object shown only on the target cell during the reveal.")]
        [SerializeField] private GameObject highlight;

        public Image ColorImage
        {
            get { AutoFill(); return colorImage; }
        }

        public void SetColor(Color color)
        {
            AutoFill();
            if (colorImage != null) colorImage.color = color;
        }

        /// <summary>Turns the reveal highlight on/off (no-op if the prefab has none).</summary>
        public void SetHighlighted(bool on)
        {
            if (highlight != null) highlight.SetActive(on);
        }

        private void AutoFill()
        {
            if (colorImage == null) colorImage = GetComponent<Image>();
        }

        private void Awake()
        {
            AutoFill();
            if (highlight != null) highlight.SetActive(false);
        }

        private void Reset() => AutoFill();
    }
}
