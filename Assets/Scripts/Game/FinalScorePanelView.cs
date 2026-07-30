using System.Collections.Generic;
using UnityEngine;

namespace ColorGuesser.Game
{
    /// <summary>One row of the final scoreboard, already ranked.</summary>
    public struct FinalScoreInfo
    {
        public int Position;   // 1-based; tied players share a position
        public string Name;
        public int ColorIndex;
        public int Score;
    }

    /// <summary>
    /// The end-of-match scoreboard: every player ordered by total score, highest first.
    /// Shown only once the match is finished.
    /// </summary>
    public class FinalScorePanelView : MonoBehaviour
    {
        [Tooltip("The scroll view's Content transform: cards are spawned as its children.")]
        [SerializeField] private Transform listContent;
        [SerializeField] private PlayerFinalScoreCard cardPrefab;

        private readonly List<PlayerFinalScoreCard> _cards = new List<PlayerFinalScoreCard>();

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        /// <summary>Rebuilds the scoreboard. Cards are reused between matches.</summary>
        public void Show(IList<FinalScoreInfo> scores)
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
                _cards[i].Set(s.Position, s.Name, s.ColorIndex, s.Score);
            }
        }
    }
}
