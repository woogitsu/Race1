using System;
using System.IO;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Reads and writes the save file in the app's persistent data folder. Every access is guarded:
    /// a corrupt or unreadable save never crashes the game, it just starts a fresh one.
    /// </summary>
    public sealed class SaveService
    {
        private const string FileName = "race1-save.json";
        private readonly string _path;

        public SaveService()
        {
            _path = Path.Combine(Application.persistentDataPath, FileName);
        }

        public string FilePath => _path;

        public SaveData Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    string json = File.ReadAllText(_path);
                    SaveData data = JsonUtility.FromJson<SaveData>(json);
                    if (data != null)
                    {
                        return SaveMigrator.Migrate(data);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save could not be read, starting fresh: {e.Message}");
            }
            return SaveMigrator.Migrate(new SaveData());
        }

        /// <summary>Writes to a temporary file first, so a crash mid-write keeps the previous save.</summary>
        public bool Save(SaveData data)
        {
            try
            {
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tmp, _path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save failed: {e.Message}");
                return false;
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(_path)) File.Delete(_path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Delete failed: {e.Message}");
            }
        }
    }
}
