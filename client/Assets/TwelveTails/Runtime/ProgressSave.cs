using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TwelveTails.Gameplay
{
    [Serializable]
    public sealed class ProgressSaveData
    {
        public int schemaVersion = 1;
        public string characterId = "character.prototype";
        public int experience;
        public int potionCount;
        public bool questComplete;
        public bool rewardClaimed;
        public string checksum = string.Empty;
    }

    public static class ProgressSave
    {
        public const int CurrentSchemaVersion = 1;

        public static void Write(string path, ProgressSaveData data)
        {
            ValidateValues(data);
            data.schemaVersion = CurrentSchemaVersion;
            data.checksum = CalculateChecksum(data);
            var temporary = path + ".tmp";
            var backup = path + ".bak";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(JsonUtility.ToJson(data, true));
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(path))
            {
                File.Replace(temporary, path, backup, true);
                if (File.Exists(backup)) File.Delete(backup);
            }
            else File.Move(temporary, path);
        }

        public static ProgressSaveData Read(string path)
        {
            var data = JsonUtility.FromJson<ProgressSaveData>(File.ReadAllText(path, Encoding.UTF8));
            if (data == null) throw new InvalidDataException("Save file is empty or invalid JSON.");
            if (data.schemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException($"Unsupported save schema: {data.schemaVersion}");
            ValidateValues(data);
            var expected = CalculateChecksum(data);
            if (!string.Equals(expected, data.checksum, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Save checksum mismatch.");
            return data;
        }

        public static string CalculateChecksum(ProgressSaveData data)
        {
            var canonical = $"{data.schemaVersion}|{data.characterId}|{data.experience}|{data.potionCount}|{data.questComplete}|{data.rewardClaimed}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var value in hash) hex.Append(value.ToString("x2"));
            return hex.ToString();
        }

        private static void ValidateValues(ProgressSaveData data)
        {
            if (string.IsNullOrWhiteSpace(data.characterId) || data.characterId.Length > 64)
                throw new InvalidDataException("Invalid character ID.");
            if (data.experience < 0 || data.potionCount < 0)
                throw new InvalidDataException("Progress values cannot be negative.");
        }
    }
}
