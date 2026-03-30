#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexusBuildPro.Editor.Data
{
    /// <summary>Persistent build history serialized to EditorPrefs/JSON.</summary>
    [Serializable]
    public sealed class BuildHistoryData
    {
        public List<BuildHistoryEntry> Entries = new();
        private const int MaxEntries = 100;

        public void AddEntry(BuildHistoryEntry entry)
        {
            Entries.Insert(0, entry);
            if (Entries.Count > MaxEntries)
                Entries.RemoveRange(MaxEntries, Entries.Count - MaxEntries);
        }

        public static BuildHistoryData Load()
        {
            var json = UnityEditor.EditorPrefs.GetString("NexusBuildPro_History", "");
            if (string.IsNullOrEmpty(json)) return new BuildHistoryData();
            try { return JsonUtility.FromJson<BuildHistoryData>(json) ?? new BuildHistoryData(); }
            catch { return new BuildHistoryData(); }
        }

        public void Save() =>
            UnityEditor.EditorPrefs.SetString("NexusBuildPro_History", JsonUtility.ToJson(this));
    }

    [Serializable]
    public sealed class BuildHistoryEntry
    {
        public string SessionId;
        public string ProfileName;
        public string PlatformId;
        public string OutputPath;
        public bool Success;
        public float DurationSeconds;
        public long OutputSizeBytes;
        public string ErrorMessage;
        public string Timestamp;
        public string Version;

        public DateTime ParsedTime =>
            DateTime.TryParse(Timestamp, out var dt) ? dt : DateTime.MinValue;

        public string FormatSize()
        {
            if (OutputSizeBytes <= 0) return "N/A";
            string[] units = { "B", "KB", "MB", "GB" };
            double size = OutputSizeBytes;
            int idx = 0;
            while (size >= 1024 && idx < units.Length - 1) { size /= 1024; idx++; }
            return $"{size:F2} {units[idx]}";
        }
    }
}
#endif
