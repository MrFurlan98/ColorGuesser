using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// A "− value +" stepper for picking one of an ordered list of numbers. Better than
    /// a dropdown for short numeric ranges: the value is always visible and changing it
    /// is a single click.
    ///
    /// When not interactable the arrows hide, so it reads as a read-only display -
    /// which is exactly what non-host players should see.
    /// </summary>
    public class StepperControl : MonoBehaviour
    {
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private TextMeshProUGUI valueText;

        private int[] _values = { 0 };
        private string _format = "{0}";
        private int _index;

        /// <summary>Raised with the new value when the user steps it.</summary>
        public event Action<int> ValueChanged;

        public int Value => _values[Mathf.Clamp(_index, 0, _values.Length - 1)];

        private void Awake()
        {
            if (minusButton != null) minusButton.onClick.AddListener(() => Step(-1));
            if (plusButton != null) plusButton.onClick.AddListener(() => Step(1));
            Redraw();
        }

        /// <summary>Sets the selectable values and how they are displayed, e.g. "{0} jogadores".</summary>
        public void Configure(int[] values, string format)
        {
            if (values != null && values.Length > 0) _values = values;
            if (!string.IsNullOrEmpty(format)) _format = format;
            _index = Mathf.Clamp(_index, 0, _values.Length - 1);
            Redraw();
        }

        /// <summary>Selects a value without raising ValueChanged (used to show synced state).</summary>
        public void SetValue(int value)
        {
            int found = Array.IndexOf(_values, value);
            if (found >= 0) _index = found;
            Redraw();
        }

        /// <summary>Host can step; everyone else just sees the value.</summary>
        public void SetInteractable(bool value)
        {
            if (minusButton != null) minusButton.gameObject.SetActive(value);
            if (plusButton != null) plusButton.gameObject.SetActive(value);
        }

        private void Step(int direction)
        {
            int next = Mathf.Clamp(_index + direction, 0, _values.Length - 1);
            if (next == _index) return;
            _index = next;
            Redraw();
            ValueChanged?.Invoke(Value);
        }

        private void Redraw()
        {
            if (valueText != null) valueText.text = string.Format(_format, Value);
            // Grey out the arrow at each end of the range.
            if (minusButton != null) minusButton.interactable = _index > 0;
            if (plusButton != null) plusButton.interactable = _index < _values.Length - 1;
        }
    }
}
