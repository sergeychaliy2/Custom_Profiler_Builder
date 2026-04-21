#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NexusBuildPro.Editor.Core;
using UnityEngine;

namespace NexusBuildPro.Editor.Profiling
{
    /// <summary>
    /// High-level profiler that wraps BuildMetrics and tracks
    /// build sessions for trend analysis and graph rendering.
    /// </summary>
    public sealed class BuildProfiler
    {
        #region Fields
        private readonly List<ProfilerSession> _sessions = new();
        private ProfilerSession _activeSession;
        #endregion

        #region Properties
        public BuildMetrics Metrics => _activeSession?.Metrics;
        public IReadOnlyList<ProfilerSession> Sessions => _sessions;
        public bool IsActive => _activeSession != null;
        public ProfilerSession ActiveSession => _activeSession;
        #endregion

        #region Public Methods
        public void Begin(string sessionId)
        {
            _activeSession = new ProfilerSession(sessionId);
            _activeSession.Metrics.StartTotal();
        }

        public void End()
        {
            if (_activeSession == null) return;
            _activeSession.Metrics.StopTotal();
            _activeSession.EndTime = DateTime.UtcNow;
            _sessions.Add(_activeSession);

            // Keep last 50 sessions in memory
            if (_sessions.Count > 50)
                _sessions.RemoveAt(0);

            _activeSession = null;
        }

        /// <summary>Returns build time trend data for the last N sessions.</summary>
        public float[] GetBuildTimeTrend(int maxSessions = 20)
        {
            int count = Math.Min(_sessions.Count, maxSessions);
            var result = new float[count];
            int start = _sessions.Count - count;
            for (int i = 0; i < count; i++)
                result[i] = _sessions[start + i].Metrics.TotalElapsedSeconds;
            return result;
        }

        /// <summary>Returns step breakdown for the last completed session.</summary>
        public Dictionary<string, float> GetLastSessionStepBreakdown()
        {
            if (_sessions.Count == 0) return new Dictionary<string, float>();
            return new Dictionary<string, float>(_sessions[^1].Metrics.StepDurations);
        }

        /// <summary>Returns average build time across all tracked sessions.</summary>
        public float GetAverageBuildTime()
        {
            if (_sessions.Count == 0) return 0f;
            float total = 0f;
            foreach (var s in _sessions) total += s.Metrics.TotalElapsedSeconds;
            return total / _sessions.Count;
        }

        public float GetFastestBuildTime()
        {
            if (_sessions.Count == 0) return 0f;
            float min = float.MaxValue;
            foreach (var s in _sessions)
                if (s.Metrics.TotalElapsedSeconds < min)
                    min = s.Metrics.TotalElapsedSeconds;
            return min == float.MaxValue ? 0f : min;
        }

        public float GetSlowestBuildTime()
        {
            if (_sessions.Count == 0) return 0f;
            float max = float.MinValue;
            foreach (var s in _sessions)
                if (s.Metrics.TotalElapsedSeconds > max)
                    max = s.Metrics.TotalElapsedSeconds;
            return max == float.MinValue ? 0f : max;
        }
        #endregion
    }

    /// <summary>Represents one complete build session's profiling data.</summary>
    public sealed class ProfilerSession
    {
        public string SessionId { get; }
        public DateTime StartTime { get; } = DateTime.UtcNow;
        public DateTime EndTime { get; set; }
        public BuildMetrics Metrics { get; } = new BuildMetrics();
        public TimeSpan Duration => EndTime - StartTime;

        public ProfilerSession(string sessionId) => SessionId = sessionId;
    }
}
#endif
