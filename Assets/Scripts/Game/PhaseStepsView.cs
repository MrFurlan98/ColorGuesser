using ColorGuesser.Core;
using UnityEngine;

namespace ColorGuesser.Game
{
    /// <summary>
    /// The "Phases" strip: Clue 1 → Guess 1 → Clue 2 → Guess 2 → Reveal. Each step
    /// paints itself (see PhaseStepController); this just says which one is current and
    /// hides the whole strip when no round is running.
    /// </summary>
    public class PhaseStepsView : MonoBehaviour
    {
        [Tooltip("The 5 steps in order. Each one owns its own colours.")]
        [SerializeField] private PhaseStepController[] steps;

        [Tooltip("Container to hide when no round is running. Empty = this GameObject.")]
        [SerializeField] private GameObject root;

        public void SetPhase(MatchPhase phase)
        {
            int current = StepIndex(phase); // < 0 means menu/lobby or match over

            var target = root != null ? root : gameObject;
            if (target.activeSelf != (current >= 0)) target.SetActive(current >= 0);
            if (current < 0 || steps == null) return;

            for (int i = 0; i < steps.Length; i++)
                if (steps[i] != null)
                    steps[i].SetCurrent(i == current);
        }

        /// <summary>Position of a phase in the 5-step strip, or -1 if it has no step.</summary>
        private static int StepIndex(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.CueMasterClue1: return 0;
                case MatchPhase.Guessing1: return 1;
                case MatchPhase.CueMasterClue2: return 2;
                case MatchPhase.Guessing2: return 3;
                case MatchPhase.Reveal: return 4;
                default: return -1; // NotStarted / Finished
            }
        }
    }
}
