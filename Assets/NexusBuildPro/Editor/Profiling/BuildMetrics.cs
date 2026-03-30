#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NexusBuildPro.Editor.Profiling
{
    /// <summary>Tracks per-step timing and resource metrics during a build.</summary>
    public sealed class BuildMetrics
    {
        #region Fields
        private readonly Dictionary<string, Stopwatch> _activeTimers = new();
        private readonly Dictionary<string, float> _completedDurations = new();
        private readonly List<MetricSample> _samples = new();
        private readonly Stopwatch _totalWatch = new();
        #endregion

        #region Properties
        public float TotalElapsedSeconds => (float)_totalWatch.Elapsed.TotalSeconds;
        public IReadOnlyDictionary<string, float> StepDurations => _completedDurations;
        public IReadOnlyList<MetricSample> Samples => _samples;
        #endregion

        #region Public Methods
        public void StartTotal() => _totalWatch.Restart();
        public void StopTotal() => _totalWatch.Stop();

        public void BeginStep(string stepName)
        {
            if (!_activeTimers.ContainsKey(stepName))
                _activeTimers[stepName] = new Stopwatch();
            _activeTimers[stepName].Restart();
        }

        public float EndStep(string stepName)
        {
            if (!_activeTimers.TryGetValue(stepName, out var sw)) return 0f;
            sw.Stop();
            float duration = (float)sw.Elapsed.TotalSeconds;
            _completedDurations[stepName] = duration;
            return duration;
        }

        public void RecordSample(string key, float value, MetricUnit unit) =>
            _samples.Add(new MetricSample(key, value, unit, DateTime.UtcNow));

        public float GetStepDuration(string stepName) =>
            _completedDurations.TryGetValue(stepName, out var d) ? d : 0f;

        public float GetTotalStepsDuration()
        {
            float total = 0;
            foreach (var d in _completedDurations.Values) total += d;
            return total;
        }
        #endregion
    }

    public enum MetricUnit { Seconds, Megabytes, Count, Percent }

    public sealed class MetricSample
    {
        public string Key { get; }
        public float Value { get; }
        public MetricUnit Unit { get; }
        public DateTime Timestamp { get; }

        public MetricSample(string key, float value, MetricUnit unit, DateTime timestamp)
        {
            Key = key; Value = value; Unit = unit; Timestamp = timestamp;
        }
    }
}
#endif
