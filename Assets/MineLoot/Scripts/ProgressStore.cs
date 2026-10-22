using System;
using UnityEngine;

namespace MineLoot
{
    public static class ProgressStore
    {
        private const string Key = "MineLoot.Demo.Progress.v1";

        public static MineGame Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return new MineGame();
            try
            {
                var data = JsonUtility.FromJson<MineProgress>(PlayerPrefs.GetString(Key));
                if (data == null || data.version != 1) throw new FormatException("Unknown save version");
                return new MineGame(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Mine Loot: invalid local save. Starting a fresh demo. " + e.Message);
                return new MineGame();
            }
        }

        public static void Save(MineGame game)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(game.Progress));
            PlayerPrefs.Save();
        }
    }
}
