using UnityEngine;

namespace AquaGames.League
{
    // Swap this to store the league in your own save system or cloud save.
    public interface ILeagueStorage
    {
        LeagueSave Load();
        void Save(LeagueSave data);
    }

    public class PlayerPrefsLeagueStorage : ILeagueStorage
    {
        readonly string key;
        public PlayerPrefsLeagueStorage(string key = "aquagames_league_v1") { this.key = key; }

        public LeagueSave Load()
        {
            var json = PlayerPrefs.GetString(key, "");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<LeagueSave>(json);
        }

        public void Save(LeagueSave data)
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }

    // In-memory storage for tests.
    public class MemoryLeagueStorage : ILeagueStorage
    {
        LeagueSave data;
        public LeagueSave Load() => data;
        public void Save(LeagueSave d) => data = JsonUtility.FromJson<LeagueSave>(JsonUtility.ToJson(d));
    }
}
