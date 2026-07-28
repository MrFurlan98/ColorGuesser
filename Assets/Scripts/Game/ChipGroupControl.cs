using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// A row of segmented "chips" (toggles) where every option is visible at once and
    /// picking one is a single click - ideal for a handful of choices such as the guess
    /// time limit. Uses Unity Toggles, so a ToggleGroup gives exclusivity and the
    /// selected look for free.
    ///
    /// Extra chips beyond the configured values are hidden automatically.
    /// </summary>
    public class ChipGroupControl : MonoBehaviour
    {
        [Tooltip("The chip toggles, in order. Put them all in one ToggleGroup.")]
        [SerializeField] private Toggle[] chips;

        [Header("Selected / unselected look")]
        [SerializeField] private Color selectedColor = new Color(0.20f, 0.55f, 0.90f);
        [SerializeField] private Color normalColor = new Color(0.28f, 0.30f, 0.36f);
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color normalTextColor = new Color(0.75f, 0.78f, 0.85f);

        private int[] _values = { 0 };

        /// <summary>Raised with the new value when the user picks a chip.</summary>
        public event Action<int> ValueChanged;

        public int Value
        {
            get
            {
                for (int i = 0; i < chips.Length && i < _values.Length; i++)
                    if (chips[i] != null && chips[i].isOn) return _values[i];
                return _values[0];
            }
        }

        private void Awake()
        {
            if (chips == null) return;
            for (int i = 0; i < chips.Length; i++)
            {
                int index = i; // capture per iteration
                if (chips[i] == null) continue;

                // We tint the chips ourselves from isOn, so stop Selectable from
                // fighting us over the background colour (its "selected" state is
                // keyboard focus, not the toggle's on/off state).
                chips[i].transition = Selectable.Transition.None;

                chips[i].onValueChanged.AddListener(on =>
                {
                    Redraw();
                    if (on && index < _values.Length) ValueChanged?.Invoke(_values[index]);
                });
            }
            Redraw();
        }

        /// <summary>Paints every chip according to whether it is the selected one.</summary>
        private void Redraw()
        {
            if (chips == null) return;
            foreach (var chip in chips)
            {
                if (chip == null) continue;
                bool on = chip.isOn;

                var background = chip.targetGraphic as Image ?? chip.GetComponent<Image>();
                if (background != null) background.color = on ? selectedColor : normalColor;

                var label = chip.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.color = on ? selectedTextColor : normalTextColor;
            }
        }

        /// <summary>Sets the values and chip labels, e.g. "{0}s". Pass zeroLabel for a 0 value.</summary>
        public void Configure(int[] values, string format, string zeroLabel = null)
        {
            if (values != null && values.Length > 0) _values = values;
            if (chips == null) return;

            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i] == null) continue;
                bool used = i < _values.Length;
                chips[i].gameObject.SetActive(used);
                if (!used) continue;

                var label = chips[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = (_values[i] == 0 && zeroLabel != null)
                        ? zeroLabel
                        : string.Format(format, _values[i]);
            }
            Redraw();
        }

        /// <summary>Selects a value without raising ValueChanged.</summary>
        public void SetValue(int value)
        {
            if (chips == null) return;
            int target = Array.IndexOf(_values, value);
            for (int i = 0; i < chips.Length; i++)
                if (chips[i] != null) chips[i].SetIsOnWithoutNotify(i == target);
            Redraw(); // SetIsOnWithoutNotify skips the change event, so repaint here
        }

        /// <summary>Host can pick; everyone else just sees the choice.</summary>
        public void SetInteractable(bool value)
        {
            if (chips == null) return;
            foreach (var chip in chips)
                if (chip != null) chip.interactable = value;
        }
    }
}
