using System;
using System.IO;
using UnityEngine;

namespace Diceforge.Audio
{
    public sealed class PlayerMusicPrefsStorage
    {
        private const string FileName = "player_music_prefs.json";

        private readonly string _filePath;
        private readonly string _tempFilePath;

        public PlayerMusicPrefsStorage()
        {
            _filePath = Path.Combine(Application.persistentDataPath, FileName);
            _tempFilePath = _filePath + ".tmp";
        }

        public PlayerMusicPrefs LoadOrCreate()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new PlayerMusicPrefs();

                string json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new PlayerMusicPrefs();

                PlayerMusicPrefs prefs = JsonUtility.FromJson<PlayerMusicPrefs>(json);
                return prefs ?? new PlayerMusicPrefs();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerMusicPrefsStorage] Failed to load prefs, using defaults. {ex.Message}");
                return new PlayerMusicPrefs();
            }
        }

        public void Save(PlayerMusicPrefs prefs)
        {
            if (prefs == null)
                return;

            try
            {
                string json = JsonUtility.ToJson(prefs, true);
                File.WriteAllText(_tempFilePath, json);

                if (File.Exists(_filePath))
                {
                    try
                    {
                        File.Replace(_tempFilePath, _filePath, null);
                    }
                    catch
                    {
                        File.Delete(_filePath);
                        File.Move(_tempFilePath, _filePath);
                    }
                }
                else
                {
                    File.Move(_tempFilePath, _filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerMusicPrefsStorage] Failed to save prefs. {ex.Message}");
            }
        }
    }
}
