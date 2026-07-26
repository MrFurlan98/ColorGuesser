using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// Passive view for the main menu (Humble Object / MVP): nickname, Host, Join by
    /// code, and Play Offline. It holds no logic - MenuController wires the events to
    /// the session. Lives on the MenuHud prefab so it can be restyled freely.
    /// </summary>
    public class MenuHud : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button hostButton;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button offlineButton;
        [SerializeField] private TextMeshProUGUI statusText;

        public event Action HostClicked;
        public event Action JoinClicked;
        public event Action OfflineClicked;
        public event Action<string> NicknameChanged;

        private void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(() => HostClicked?.Invoke());
            if (joinButton != null) joinButton.onClick.AddListener(() => JoinClicked?.Invoke());
            if (offlineButton != null) offlineButton.onClick.AddListener(() => OfflineClicked?.Invoke());
            if (nicknameInput != null) nicknameInput.onValueChanged.AddListener(v => NicknameChanged?.Invoke(v));
        }

        public string JoinCode => joinCodeInput != null ? joinCodeInput.text : string.Empty;

        public void SetNickname(string nickname) { if (nicknameInput != null) nicknameInput.text = nickname; }
        public void SetStatus(string text) { if (statusText != null) statusText.text = text; }

        public void SetInteractable(bool value)
        {
            if (hostButton != null) hostButton.interactable = value;
            if (joinButton != null) joinButton.interactable = value;
            if (offlineButton != null) offlineButton.interactable = value;
        }

        public void SetVisible(bool visible) { gameObject.SetActive(visible); }
    }
}
