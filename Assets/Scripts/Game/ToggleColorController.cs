using System;
using ColorGuesser.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ColorGuesser.Game
{
    /// <summary>
    /// One colour option in the menu. Put this on each colour Toggle: it owns which
    /// palette colour the toggle represents, tints its own swatch, and reports when it
    /// is picked. MenuHud just collects these instead of managing toggles + images.
    ///
    /// Set "Color Index" in the inspector - the swatch updates live in edit mode, so
    /// you can see the real colours while laying the prefab out.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class ToggleColorController : MonoBehaviour
    {
        [Tooltip("Index into PlayerPalette (0-9) that this toggle offers.")]
        [SerializeField] private int colorIndex;

        [Tooltip("The Toggle itself. Auto-filled from this GameObject if left empty.")]
        [SerializeField] private Toggle toggle;

        [Tooltip("The Image tinted with the colour. Auto-filled from a child named " +
                 "\"Color\", or the toggle's target graphic, if left empty.")]
        [SerializeField] private Image swatch;
        [SerializeField] private Image bg;

        /// <summary>Raised with this option's palette index when the toggle is switched on.</summary>
        public event Action<int> Selected;

        public int ColorIndex => colorIndex;

        private void Awake()
        {
            AutoFill();
            Apply();
            if (toggle != null)
                toggle.onValueChanged.AddListener(on => { if (on) Selected?.Invoke(colorIndex); });
        }

        /// <summary>Ticks/unticks without firing events (used to restore the saved pick).</summary>
        public void SetOn(bool on)
        {
            if (toggle == null) toggle = GetComponent<Toggle>();
            if (toggle != null) toggle.SetIsOnWithoutNotify(on);
        }

        /// <summary>Tints the swatch with this option's palette colour.</summary>
        public void Apply()
        {
            if (swatch != null) swatch.color = PlayerPalette.Get(colorIndex);
            if (bg != null) bg.color = PlayerPalette.Get(colorIndex);
        }

        private void AutoFill()
        {
            if (toggle == null) toggle = GetComponent<Toggle>();
            if (toggle != null) bg = toggle.targetGraphic as Image;
            if (swatch != null) return;

            // Prefer a descendant named "Color" (at any depth), else the target graphic.
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child.name == "Color")
                {
                    var image = child.GetComponent<Image>();
                    if (image != null) { swatch = image; return; }
                }

            if (toggle != null) swatch = toggle.targetGraphic as Image;

        }

        // Live preview while editing the prefab: change Color Index and see the swatch.
        private void OnValidate()
        {
            colorIndex = PlayerPalette.Clamp(colorIndex);
            AutoFill();
            Apply();
        }

        private void Reset()
        {
            AutoFill();
            Apply();
        }
    }
}
