namespace ColorGuesser.Core
{
    /// <summary>
    /// The steps a round moves through, in order. The UI shows different things in
    /// each phase, and the state machine only accepts the matching action per phase.
    /// </summary>
    public enum MatchPhase
    {
        NotStarted,      // before StartMatch
        CueMasterClue1,  // cue master sees the secret color and gives the 1st clue word
        Guessing1,       // every other player places their 1st guess
        CueMasterClue2,  // cue master gives the 2nd clue word
        Guessing2,       // every other player places their 2nd guess
        Reveal,          // target shown, scores applied; waiting for NextRound
        Finished         // all rounds played
    }
}
