using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ColorGuesser.Core;
using Unity.Services.CloudSave;
using UnityEngine;

namespace ColorGuesser.Net
{
    /// <summary>
    /// Stores this player's lifetime statistics in Cloud Save, keyed on their
    /// Authentication account (the C4 "Cloud Save" container). Each player saves only
    /// their own record, so there is no authority question: the host does not write other
    /// people's data.
    ///
    /// Guests are skipped entirely - not stored and not loaded - which is what makes the
    /// "play as guest" choice mean something rather than being a label.
    /// </summary>
    public class PlayerStatsStore : MonoBehaviour
    {
        private const string StatsKey = "playerStats";

        [Tooltip("Used to know whether the player is a guest and whether they are signed in.")]
        [SerializeField] private SessionBootstrap session;

        private PlayerStats _cached;
        private bool _busy;

        /// <summary>The stats last loaded or saved, or null if none are known yet.</summary>
        public PlayerStats Cached => _cached;

        /// <summary>Raised after a successful load or save, so UI can refresh.</summary>
        public event Action Changed;

        private bool CanUseCloud =>
            session != null && session.CanStoreData && !string.IsNullOrEmpty(session.PlayerId);

        /// <summary>
        /// Reads the player's record. Returns fresh, empty stats for a guest (or on any
        /// failure) so callers never have to deal with a null.
        /// </summary>
        public async Task<PlayerStats> LoadAsync()
        {
            if (!CanUseCloud) return _cached = new PlayerStats();

            try
            {
                var keys = new HashSet<string> { StatsKey };
                var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                _cached = loaded.TryGetValue(StatsKey, out var item)
                    ? JsonUtility.FromJson<PlayerStats>(item.Value.GetAsString()) ?? new PlayerStats()
                    : new PlayerStats();
            }
            catch (Exception e)
            {
                // Never let a storage problem stop someone playing.
                Debug.LogWarning("Could not load player stats: " + e);
                _cached = new PlayerStats();
            }

            Changed?.Invoke();
            return _cached;
        }

        /// <summary>
        /// Folds a finished match into the player's record and saves it. Does nothing for
        /// a guest. Loads first if the record has not been read yet this session, so a
        /// fresh install does not overwrite existing progress with a single match.
        /// </summary>
        public async Task RecordMatchAsync(int finalScore, bool won, int rounds)
        {
            // Saying WHY nothing was saved, rather than returning in silence: without this
            // an unsaved match and a disabled service look identical from the outside.
            if (session == null)
            {
                Debug.LogWarning("Stats not saved: PlayerStatsStore has no session assigned.");
                return;
            }
            if (!session.CanStoreData)
            {
                Debug.Log("Stats not saved: playing as a guest, which stores nothing.");
                return;
            }
            if (string.IsNullOrEmpty(session.PlayerId))
            {
                Debug.LogWarning("Stats not saved: not signed in, so there is no account to save to.");
                return;
            }
            if (_busy) return;
            _busy = true;

            try
            {
                if (_cached == null) await LoadAsync();

                var stats = _cached ?? new PlayerStats();
                stats.AddMatch(finalScore, won, rounds, DateTime.UtcNow);

                var data = new Dictionary<string, object> { { StatsKey, JsonUtility.ToJson(stats) } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);

                _cached = stats;
                Debug.Log($"Stats saved for {session.PlayerId}: {stats.matchesPlayed} match(es), " +
                          $"{stats.matchesWon} won, {stats.totalPoints} point(s), best {stats.bestScore}.");
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not save player stats: " + e);
            }
            finally { _busy = false; }
        }
    }
}
