using HuesNCues.Core;
using TMPro;
using UnityEngine;

namespace HuesNCues.Game
{
    /// <summary>
    /// The "Titles" block: round number, phase title and subtitle. All the wording
    /// lives here as editable fields, next to the texts that show it.
    /// </summary>
    public class RoundTitlesView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;

        [Header("Labels (editable wording)")]
        [SerializeField] private string roundFormat = "RODADA {0}";
        [SerializeField] private string matchOverLabel = "FIM DA PARTIDA";
        [SerializeField] private string cueMasterTitle = "Você é o mestre da cor";
        [SerializeField] private string guesserTitle = "Encontre a cor";
        [SerializeField] private string revealTitle = "Revelação da cor";
        [SerializeField] private string finishedTitle = "Placar final";

        [TextArea] [SerializeField] private string cueMasterSubtitle =
            "Só você pode ver a cor-alvo desta rodada.";
        [TextArea] [SerializeField] private string guesserSubtitle =
            "Use a dica para escolher a tonalidade mais provável.";
        [TextArea] [SerializeField] private string revealSubtitle =
            "Veja onde cada jogador apostou e como os pontos foram calculados.";
        [TextArea] [SerializeField] private string finishedSubtitle =
            "A partida terminou. Hora de descobrir quem dominou o espectro.";

        /// <summary>Shows the round number, or the end-of-match label once it is over.</summary>
        public void SetRound(int round, bool matchOver)
        {
            if (roundText == null) return;
            roundText.text = matchOver ? matchOverLabel : string.Format(roundFormat, round);
        }

        /// <summary>
        /// Title and subtitle for the current phase, from this player's point of view:
        /// during the clue/guessing phases they depend on whether you are the cue master;
        /// the reveal and the end of the match read the same for everyone.
        /// </summary>
        public void SetPhaseTexts(MatchPhase phase, bool isCueMaster)
        {
            string title, subtitle;
            switch (phase)
            {
                case MatchPhase.Reveal:
                    title = revealTitle; subtitle = revealSubtitle;
                    break;
                case MatchPhase.Finished:
                    title = finishedTitle; subtitle = finishedSubtitle;
                    break;
                default: // clue + guessing phases
                    title = isCueMaster ? cueMasterTitle : guesserTitle;
                    subtitle = isCueMaster ? cueMasterSubtitle : guesserSubtitle;
                    break;
            }

            if (titleText != null) titleText.text = title;
            if (subtitleText != null) subtitleText.text = subtitle;
        }
    }
}
