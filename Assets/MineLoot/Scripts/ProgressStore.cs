using System;
using UnityEngine;

namespace MineLoot
{
    public static class ProgressStore
    {
        // The storage key stays stable so v1/v2 installations are upgraded in place.
        public const string Key = "MineLoot.Progress.v2";
        private const string LegacyKey = "MineLoot.Demo.Progress.v1";

        public static MineGame Load(string key = Key)
        {
            // A synchronous browser journal closes the gap before IndexedDB finishes flushing.
            string journal = BrowserPlatform.ReadProgress(key);
            if (!string.IsNullOrEmpty(journal))
            {
                try { return new MineGame(Decode(journal)); }
                catch (Exception) { Debug.LogWarning("Lucky Pickaxe: invalid browser journal; trying PlayerPrefs."); }
            }
            string[] candidates = key == Key ? new[] { key, key + ".backup", LegacyKey } : new[] { key, key + ".backup" };
            foreach (string candidate in candidates)
            {
                if (!PlayerPrefs.HasKey(candidate)) continue;
                try
                {
                    var data = Decode(PlayerPrefs.GetString(candidate));
                    return new MineGame(data);
                }
                catch (Exception) { Debug.LogWarning("Lucky Pickaxe: invalid save; trying backup."); }
            }
            return new MineGame();
        }

        public static MineProgress Decode(string json)
        {
            var data = JsonUtility.FromJson<MineProgress>(json);
            if (data == null || data.version < 1 || data.version > 11) throw new FormatException("Unknown save version");
            return data;
        }

        public static void Save(MineGame game, string key = Key)
        {
            // Both copies contain the paid pending draw, or the completed reward transaction.
            string json = JsonUtility.ToJson(game.Progress);
            BrowserPlatform.WriteProgress(key, json);
            PlayerPrefs.SetString(key + ".backup", json);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }
    }
}
