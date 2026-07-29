using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>One player's result for the round just revealed.</summary>
    public struct RoundScoreInfo
    {
        public string Name;
        public int ColorIndex;
        public int RoundScore;
    }

    /// <summary>
    /// The reveal score panel: what everyone scored this round, ordered highest first,
    /// plus a "next round" button every player presses. The round advances once all of
    /// them have pressed it, or when the countdown runs out.
    /// </summary>
    public class ScorePanelView : MonoBehaviour
    {
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI nextLabel;

        [Header("Score list")]
        [Tooltip("The scroll view's Content transform: cards are spawned as its children.")]
        [SerializeField] private Transform listContent;
        [SerializeField] private PlayerRoundScoreCard cardPrefab;

        [Header("Labels")]
        [SerializeField] private string nextRoundLabel = "PRÓXIMA RODADA";
        [SerializeField] private string finishLabel = "VER PLACAR FINAL";

        [Tooltip("Shown after you press next, while the other players catch up.")]
        [SerializeField] private string waitingLabel = "ESPERANDO...";

        private readonly List<PlayerRoundScoreCard> _cards = new List<PlayerRoundScoreCard>();

        /// <summary>Raised when this player presses the next/finish button.</summary>
        public event Action NextClicked;

        private void Awake()
        {
            if (nextButton != null) nextButton.onClick.AddListener(() => NextClicked?.Invoke());
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        /// <summary>
        /// Fills the panel: scores ordered highest first, plus the button. Once you have
        /// pressed it the button is disabled and reads "waiting", until the other players
        /// catch up (or the reveal times out). When the match is decided it finishes the
        /// game instead, with nobody to wait for.
        /// </summary>
        public void Show(IList<RoundScoreInfo> scores, bool matchDecided, bool alreadyVoted)
        {
            bool waiting = alreadyVoted && !matchDecided;

            if (nextLabel != null)
                nextLabel.text = waiting ? waitingLabel
                               : matchDecided ? finishLabel
                               : nextRoundLabel;
            if (nextButton != null) nextButton.interactable = !alreadyVoted;

            BuildList(scores);
        }

        private void BuildList(IList<RoundScoreInfo> scores)
        {
            if (listContent == null || cardPrefab == null) return;

            while (_cards.Count < scores.Count)
                _cards.Add(Instantiate(cardPrefab, listContent));

            for (int i = 0; i < _cards.Count; i++)
            {
                bool used = i < scores.Count;
                _cards[i].gameObject.SetActive(used);
                if (!used) continue;

                var s = scores[i];
                _cards[i].Set(s.Name, s.ColorIndex, s.RoundScore);
            }
        }
    }
}
