using TMPro;
using UnityEngine;

namespace HuesNCues.Game
{
    /// <summary>
    /// The title/subtitle inside GameInfo, which reads differently for the cue master
    /// and for everyone else. Separate from the main RoundTitlesView (the "Titles"
    /// block), which describes the phase rather than what you personally have to do.
    /// </summary>
    public class RoleTitlesView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;

        [Header("Cue master")]
        [SerializeField] private string cueMasterTitle = "Mestre da cor";
        [TextArea] [SerializeField] private string cueMasterSubtitle =
            "Escreva uma dica de uma palavra para a cor secreta.";

        [Header("Other players")]
        [SerializeField] private string guesserTitle = "Seu palpite";
        [TextArea] [SerializeField] private string guesserSubtitle =
            "Escolha uma cor no tabuleiro e confirme.";

        /// <summary>Switches the wording to match this player's role in the round.</summary>
        public void SetRole(bool isCueMaster)
        {
            if (titleText != null)
                titleText.text = isCueMaster ? cueMasterTitle : guesserTitle;
            if (subtitleText != null)
                subtitleText.text = isCueMaster ? cueMasterSubtitle : guesserSubtitle;
        }
    }
}
