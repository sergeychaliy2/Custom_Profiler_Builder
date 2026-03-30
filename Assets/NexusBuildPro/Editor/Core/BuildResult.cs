#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.Build.Reporting;

namespace NexusBuildPro.Editor.Core
{
    /// <summary>Immutable result of a completed build operation.</summary>
    public sealed class BuildResult
    {
        #region Properties
        public bool Success { get; }
        public string PlatformId { get; }
        public string OutputPath { get; }
        public TimeSpan TotalDuration { get; }
        public DateTime BuildTime { get; }
        public long OutputSizeBytes { get; }
        public string ErrorMessage { get; }
        public BuildReport UnityReport { get; }
        public IReadOnlyList<BuildLogEntry> Log { get; }
        public IReadOnlyDictionary<string, float> StepDurations { get; }
        public string SessionId { get; }
        #endregion

        #region Constructor
        public BuildResult(
            bool success,
            string platformId,
            string outputPath,
            TimeSpan totalDuration,
            DateTime buildTime,
            long outputSizeBytes,
            string errorMessage,
            BuildReport unityReport,
            IReadOnlyList<BuildLogEntry> log,
            IReadOnlyDictionary<string, float> stepDurations,
            string sessionId)
        {
            Success = success;
            PlatformId = platformId;
            OutputPath = outputPath;
            TotalDuration = totalDuration;
            BuildTime = buildTime;
            OutputSizeBytes = outputSizeBytes;
            ErrorMessage = errorMessage;
            UnityReport = unityReport;
            Log = log;
            StepDurations = stepDurations;
            SessionId = sessionId;
        }
        #endregion

        #region Public Methods
        public string FormatSize()
        {
            if (OutputSizeBytes <= 0) return "N/A";
            string[] units = { "B", "KB", "MB", "GB" };
            double size = OutputSizeBytes;
            int idx = 0;
            while (size >= 1024 && idx < units.Length - 1) { size /= 1024; idx++; }
            return $"{size:F2} {units[idx]}";
        }

        public string FormatDuration() =>
            TotalDuration.TotalMinutes >= 1
                ? $"{(int)TotalDuration.TotalMinutes}m {TotalDuration.Seconds}s"
                : $"{TotalDuration.TotalSeconds:F1}s";
        #endregion
    }
}
#endif
