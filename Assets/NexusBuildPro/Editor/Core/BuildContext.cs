#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;

namespace NexusBuildPro.Editor.Core
{
    /// <summary>Mutable state container passed through the entire build pipeline.</summary>
    public sealed class BuildContext
    {
        #region Properties
        public BuildProfile Profile { get; }
        public IPlatformBuildStrategy Strategy { get; }
        public CancellationToken CancellationToken { get; }
        public string SessionId { get; } = Guid.NewGuid().ToString("N")[..8];
        public DateTime StartTime { get; } = DateTime.UtcNow;

        public float Progress { get; private set; }
        public string CurrentStepName { get; private set; } = "Initializing";
        public bool HasErrors { get; private set; }

        public IReadOnlyList<BuildLogEntry> Log => _log;
        public IReadOnlyDictionary<string, object> Metadata => _metadata;
        #endregion

        #region Fields
        private readonly List<BuildLogEntry> _log = new();
        private readonly Dictionary<string, object> _metadata = new();
        #endregion

        #region Events
        public event Action<float, string> OnProgressChanged;
        public event Action<BuildLogEntry> OnLogEntry;
        #endregion

        #region Constructor
        public BuildContext(BuildProfile profile, IPlatformBuildStrategy strategy, CancellationToken cancellationToken)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            CancellationToken = cancellationToken;
        }
        #endregion

        #region Public Methods
        public void ReportProgress(float normalized, string stepName)
        {
            Progress = Math.Clamp(normalized, 0f, 1f);
            CurrentStepName = stepName;
            OnProgressChanged?.Invoke(Progress, stepName);
        }

        public void LogInfo(string message) => AddEntry(BuildLogLevel.Info, message);
        public void LogWarning(string message) => AddEntry(BuildLogLevel.Warning, message);
        public void LogError(string message) { HasErrors = true; AddEntry(BuildLogLevel.Error, message); }
        public void LogSuccess(string message) => AddEntry(BuildLogLevel.Success, message);

        public void SetMetadata(string key, object value) => _metadata[key] = value;
        public T GetMetadata<T>(string key, T defaultValue = default) =>
            _metadata.TryGetValue(key, out var val) && val is T typed ? typed : defaultValue;
        #endregion

        #region Private Methods
        private void AddEntry(BuildLogLevel level, string message)
        {
            var entry = new BuildLogEntry(level, message, DateTime.UtcNow);
            _log.Add(entry);
            OnLogEntry?.Invoke(entry);
        }
        #endregion
    }

    public enum BuildLogLevel { Info, Warning, Error, Success }

    public sealed class BuildLogEntry
    {
        public BuildLogLevel Level { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public BuildLogEntry(BuildLogLevel level, string message, DateTime timestamp)
        {
            Level = level; Message = message; Timestamp = timestamp;
        }
    }
}
#endif
