using System.Text;
using ColorGuesser.Core;
using UnityEngine;

namespace ColorGuesser.Net
{
    /// <summary>
    /// Serializable wire form of an IMatchCommand (Command pattern travelling over the
    /// network). Sent client -> host as JSON bytes, then turned back into a command
    /// and applied on the authoritative host.
    /// </summary>
    [System.Serializable]
    public class CommandDto
    {
        public int type;        // 0 = clue, 1 = guess, 2 = next round
        public string playerId;
        public string word;
        public int col;
        public int row;

        public static CommandDto From(IMatchCommand command)
        {
            switch (command)
            {
                case SubmitClueCommand c:
                    return new CommandDto { type = 0, playerId = c.PlayerId, word = c.Word ?? "" };
                case SubmitGuessCommand g:
                    return new CommandDto { type = 1, playerId = g.PlayerId, col = g.Coord.Column, row = g.Coord.Row };
                case NextRoundCommand n:
                    return new CommandDto { type = 2, playerId = n.PlayerId ?? "" };
                default:
                    return null;
            }
        }

        public IMatchCommand ToCommand()
        {
            switch (type)
            {
                case 0: return new SubmitClueCommand { PlayerId = playerId, Word = word };
                case 1: return new SubmitGuessCommand { PlayerId = playerId, Coord = new GridCoordinate(col, row) };
                case 2: return new NextRoundCommand { PlayerId = playerId };
                default: return null;
            }
        }

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
        public static CommandDto FromBytes(byte[] bytes) => JsonUtility.FromJson<CommandDto>(Encoding.UTF8.GetString(bytes));
    }
}
