using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Diceforge.Progression
{
    internal sealed class AtomicProfileStore
    {
        private readonly string _path;
        // Fault injection is per store, never a global change to the player's files.
        internal Action<string> Checkpoint;

        internal AtomicProfileStore(string path) => _path = path;

        internal PlayerProfile Load()
        {
            if (!File.Exists(_path) && !File.Exists(_path + ".bak"))
                return null;
            if (TryRead(_path, out var profile))
                return profile;
            if (TryRead(_path + ".bak", out profile))
            {
                // Preserve the valid backup when repairing a damaged primary.
                WriteTemporary(JsonUtility.ToJson(profile, true));
                if (File.Exists(_path)) File.Replace(_path + ".tmp", _path, null);
                else File.Move(_path + ".tmp", _path);
                return profile;
            }
            throw new InvalidDataException("Не удалось загрузить профиль: основной файл и резервная копия повреждены.");
        }

        internal void Save(PlayerProfile profile)
        {
            string json = JsonUtility.ToJson(profile, true);
            Validate(json);
            WriteTemporary(json);
            Checkpoint?.Invoke("BeforeReplace");
            if (File.Exists(_path)) File.Replace(_path + ".tmp", _path, _path + ".bak");
            else File.Move(_path + ".tmp", _path);
            Checkpoint?.Invoke("AfterReplace");
        }

        private void WriteTemporary(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            Checkpoint?.Invoke("BeforeWrite");
            using var stream = new FileStream(_path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
            Checkpoint?.Invoke("BeforeFlush");
            stream.Flush(true);
        }

        private static bool TryRead(string path, out PlayerProfile profile)
        {
            profile = null;
            if (!File.Exists(path)) return false;
            try { profile = Validate(File.ReadAllText(path)); return true; }
            catch (ArgumentException) { return false; }
            catch (InvalidDataException) { return false; }
        }

        internal static PlayerProfile Validate(string json)
        {
            // JsonUtility accepts {}, so check required legacy fields explicitly too.
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{") ||
                !json.Contains("\"version\"") || !json.Contains("\"hero\"") ||
                !json.Contains("\"currencies\"") || !json.Contains("\"inventory\""))
                throw new InvalidDataException("Profile structure is incomplete.");
            var profile = JsonUtility.FromJson<PlayerProfile>(json);
            if (profile == null || string.IsNullOrWhiteSpace(profile.version) || profile.hero == null ||
                profile.hero.xp < 0 || profile.currencies == null || profile.inventory == null)
                throw new InvalidDataException("Profile structure is invalid.");
            profile.chapters ??= new();
            profile.progressionReceipts ??= new();
            var chapters = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var chapter in profile.chapters)
                if (chapter == null || string.IsNullOrEmpty(chapter.chapterId) || string.IsNullOrEmpty(chapter.runId) ||
                    chapter.state == null || chapter.state.completedNodeIds == null || chapter.state.unlockedNodeIds == null ||
                    !chapters.Add(chapter.chapterId))
                    throw new InvalidDataException("Chapter progress is invalid.");
            var operations = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var receipt in profile.progressionReceipts)
                if (receipt == null || string.IsNullOrEmpty(receipt.operationId) || receipt.reward == null || !operations.Add(receipt.operationId))
                    throw new InvalidDataException("Progression receipt is invalid.");
            return profile;
        }
    }
}
