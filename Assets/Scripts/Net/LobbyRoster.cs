using System.Text;
using UnityEngine;

namespace ColorGuesser.Net
{
    /// <summary>
    /// The pre-match player list (who is connected, their nickname, colour and ready
    /// state). The host builds it and broadcasts it as JSON bytes so every client can
    /// show the same lobby before the match starts.
    /// </summary>
    [System.Serializable]
    public class LobbyRoster
    {
        public long[] clientIds;
        public string[] names;
        public int[] colorIndexes; // index into PlayerPalette, unique per player
        public bool[] ready;       // the host counts as always ready
        public long hostId = -1;   // which clientId is the host
        public LobbySettings settings = new LobbySettings(); // host-chosen room settings

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
        public static LobbyRoster FromBytes(byte[] bytes) => JsonUtility.FromJson<LobbyRoster>(Encoding.UTF8.GetString(bytes));
    }
}
