using System;
using ColorGuesser.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorGuesser.Game
{
    /// <summary>
    /// Passive view for the main menu (Humble Object / MVP): nickname, colour choice,
    /// Host, and a two-step Join (the code field stays hidden until "Join Game" is
    /// pressed). It holds no logic - MenuController wires the events to the session.
    /// Lives on the MenuHud prefab so it can be restyled freely.
    /// </summary>
    public class MenuHud : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button hostButton;

        [Header("Colour choice — drag the 10 colour toggles (each has a ToggleColorController)")]
        [SerializeField] private ToggleColorController[] colorOptions;

        [Header("Join flow")]
        [Tooltip("The button that REVEALS the join fields (your 'Join GameButton').")]
        [SerializeField] private Button showJoinButton;
        [Tooltip("Object holding the code field + confirm button; hidden until revealed.")]
        [SerializeField] private GameObject joinGroup;
        [Tooltip("The button that actually joins (your 'Join Button').")]
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_InputField joinCodeInput;

        [Header("Guest mode")]
        [Tooltip("Optional. When ON the player plays as a guest: nothing is stored and no " +
                 "statistics are kept. Leave unassigned to always store data.")]
        [SerializeField] private Toggle guestToggle;
        [Tooltip("Optional. States plainly whether progress is being saved, so the guest " +
                 "choice is visible rather than implied by a checkbox.")]
        [SerializeField] private TextMeshProUGUI storageStatusText;

        [Header("Notice")]
        [Tooltip("Optional. Explains why the player was dropped from a room (host closed " +
                 "it, connection lost, room full). Hidden when there is nothing to say.")]
        [SerializeField] private TextMeshProUGUI noticeText;

        public event Action HostClicked;
        public event Action JoinClicked;
        public event Action<string> NicknameChanged;
        public event Action<int> ColorChanged;

        /// <summary>Raised with true when the player chooses to play as a guest.</summary>
        public event Action<bool> GuestModeChanged;

        private void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(() => HostClicked?.Invoke());
            if (joinButton != null) joinButton.onClick.AddListener(() => JoinClicked?.Invoke());
            if (nicknameInput != null) nicknameInput.onValueChanged.AddListener(v => NicknameChanged?.Invoke(v));
            if (guestToggle != null) guestToggle.onValueChanged.AddListener(v => GuestModeChanged?.Invoke(v));
            if (joinCodeInput != null) joinCodeInput.onSubmit.AddListener(_ => JoinClicked?.Invoke());

            // "Join Game" reveals the code field instead of joining immediately.
            if (showJoinButton != null) showJoinButton.onClick.AddListener(ShowJoinFields);
            if (joinGroup != null) joinGroup.SetActive(false);

            // Each colour option reports its own palette index when picked.
            if (colorOptions != null)
                foreach (var option in colorOptions)
                {
                    if (option == null) continue;
                    option.Selected += index => ColorChanged?.Invoke(index);
                }
        }

        /// <summary>Reveals the join code field + confirm button (hides the reveal button).</summary>
        public void ShowJoinFields()
        {
            if (joinGroup != null) joinGroup.SetActive(true);
            if (showJoinButton != null) showJoinButton.gameObject.SetActive(false);
            if (joinCodeInput != null) joinCodeInput.Select();
        }

        public string JoinCode => joinCodeInput != null ? joinCodeInput.text : string.Empty;

        public void SetNickname(string nickname) { if (nicknameInput != null) nicknameInput.text = nickname; }

        /// <summary>Reflects the stored guest choice without re-firing the event.</summary>
        public void SetGuestMode(bool guest)
        {
            if (guestToggle != null) guestToggle.SetIsOnWithoutNotify(guest);
        }

        /// <summary>
        /// Says whether this player's results will be kept. Deliberately explicit: the
        /// guest choice only means something if the player can see its effect.
        /// </summary>
        public void SetStorageStatus(bool canStore)
        {
            if (storageStatusText == null) return;

            storageStatusText.text = canStore
                ? "Seu progresso será salvo nesta conta."
                : "Modo convidado — nada será salvo.";
        }

        /// <summary>Shows why the player left a room, or hides the line when empty.</summary>
        public void SetNotice(string message)
        {
            if (noticeText == null) return;

            bool show = !string.IsNullOrWhiteSpace(message);
            if (noticeText.gameObject.activeSelf != show) noticeText.gameObject.SetActive(show);
            if (show) noticeText.text = message;
        }

        /// <summary>Ticks the option for the given palette index (without re-firing events).</summary>
        public void SetSelectedColor(int index)
        {
            if (colorOptions == null) return;
            foreach (var option in colorOptions)
                if (option != null)
                    option.SetOn(option.ColorIndex == index);
        }

        /// <summary>Asks every colour option to tint its own swatch.</summary>
        public void ApplyPaletteColors()
        {
            if (colorOptions == null) return;
            foreach (var option in colorOptions)
                if (option != null) option.Apply();
        }

        public void SetInteractable(bool value)
        {
            if (hostButton != null) hostButton.interactable = value;
            if (joinButton != null) joinButton.interactable = value;
            if (showJoinButton != null) showJoinButton.interactable = value;
        }

        public void SetVisible(bool visible) { gameObject.SetActive(visible); }
    }
}
