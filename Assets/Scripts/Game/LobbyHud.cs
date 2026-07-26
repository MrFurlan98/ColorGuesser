using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// Passive view for the in-room lobby: the room code, the connected players, and
    /// Start / Leave. Logic lives in LobbyController. Lives on the LobbyHud prefab.
    /// </summary>
    public class LobbyHud : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI codeText;
        [SerializeField] private TextMeshProUGUI playerListText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;

        public event Action StartClicked;
        public event Action LeaveClicked;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(() => StartClicked?.Invoke());
            if (leaveButton != null) leaveButton.onClick.AddListener(() => LeaveClicked?.Invoke());
        }

        public void SetCode(string text) { if (codeText != null) codeText.text = text; }
        public void SetPlayerList(string text) { if (playerListText != null) playerListText.text = text; }
        public void SetStatus(string text) { if (statusText != null) statusText.text = text; }
        public void SetStartVisible(bool visible) { if (startButton != null) startButton.gameObject.SetActive(visible); }
        public void SetStartInteractable(bool value) { if (startButton != null) startButton.interactable = value; }
        public void SetVisible(bool visible) { gameObject.SetActive(visible); }
    }
}
