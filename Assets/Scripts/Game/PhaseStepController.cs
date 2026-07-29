using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// One step in the round progress strip (Clue 1, Guess 1, Clue 2, Guess 2, Reveal).
    /// Put this on each step image: it owns its own icon and label colours and switches
    /// between the "current step" and "not current" look.
    ///
    /// MatchHud just tells each step whether it is the current one.
    /// </summary>
    public class PhaseStepController : MonoBehaviour
    {
        [Tooltip("The step's image. Auto-filled from this GameObject if left empty.")]
        [SerializeField] private Image icon;

        [Tooltip("The text inside the image. Auto-filled from the children if left empty.")]
        [SerializeField] private TextMeshProUGUI label;

        [Header("Current step")]
        [SerializeField] private Color activeIconColor = Color.white;
        [SerializeField] private Color activeTextColor = new Color(0.12f, 0.13f, 0.16f);

        [Header("Other steps")]
        [SerializeField] private Color inactiveIconColor = new Color(0.35f, 0.37f, 0.44f);
        [SerializeField] private Color inactiveTextColor = new Color(0.75f, 0.78f, 0.85f);

        [Tooltip("Preview the current-step look while editing the prefab.")]
        [SerializeField] private bool previewAsCurrent;

        /// <summary>Paints this step as the current one, or as one of the others.</summary>
        public void SetCurrent(bool isCurrent)
        {
            AutoFill();
            if (icon != null) icon.color = isCurrent ? activeIconColor : inactiveIconColor;
            if (label != null) label.color = isCurrent ? activeTextColor : inactiveTextColor;
        }

        private void AutoFill()
        {
            if (icon == null) icon = GetComponent<Image>();
            if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void Awake() => AutoFill();

        // Live preview while laying the prefab out.
        private void OnValidate()
        {
            AutoFill();
            SetCurrent(previewAsCurrent);
        }

        private void Reset() => AutoFill();
    }
}
