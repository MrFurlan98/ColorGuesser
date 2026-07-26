using System.Text;
using UnityEngine;

namespace HuesNCues.Net
{
    /// <summary>
    /// The pre-match player list (who is connected and their nickname). The host
    /// builds it and broadcasts it as JSON bytes so every client can show the same
    /// lobby before the match starts.
    /// </summary>
    [System.Serializable]
    public class LobbyRoster
    {
        public long[] clientIds;
        public string[] names;

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
        public static LobbyRoster FromBytes(byte[] bytes) => JsonUtility.FromJson<LobbyRoster>(Encoding.UTF8.GetString(bytes));
    }
}
