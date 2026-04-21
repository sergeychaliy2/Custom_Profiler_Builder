#if UNITY_EDITOR
using System.Collections.Generic;
using NexusBuildPro.Editor.Core;
using NexusBuildPro.Editor.Profiling;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.UI.Views
{
    /// <summary>
    /// Live profiling view: step timeline Gantt chart, step breakdown bar chart,
    /// session history trend, and real-time build metrics during an active build.
    /// </summary>
    public sealed class ProfilerView
    {
        #region Fields
        private readonly BuildOrchestrator _orchestrator;
        #endregion

        #region Constructor
        public ProfilerView(BuildOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }
        #endregion

        #region Public Methods
        public void Draw(Rect rect)
        {
            float topH = rect.height * 0.5f - 4f;
            float botH = rect.height - topH - 8f;
            float halfW = rect.width * 0.5f - 4f;

            // Top row: timeline (left) + progress ring (right)
            DrawTimeline(new Rect(rect.x, rect.y, halfW * 1.3f, topH));
            DrawLiveMetrics(new Rect(rect.x + halfW * 1.3f + 8f, rect.y, rect.width - halfW * 1.3f - 8f, topH));

            // Bottom row: step bar chart (left) + trend (right)
            DrawStepBreakdown(new Rect(rect.x, rect.y + topH + 8f, halfW, botH));
            DrawSessionTrend(new Rect(rect.x + halfW + 8f, rect.y + topH + 8f, halfW, botH));
        }
        #endregion

        #region Private Methods
        private void DrawTimeline(Rect rect)
        {
            var stepDurations = _orchestrator.Profiler.GetLastSessionStepBreakdown();
            float total = _orchestrator.Profiler.GetSlowestBuildTime();
            if (total <= 0) total = 1f;
            ChartRenderer.DrawTimeline(rect, stepDurations, total, "Step Timeline (last build)");
        }

        private void DrawLiveMetrics(Rect rect)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width, 16f),
                "LIVE METRICS", NexusStyles.SectionTitle);

            var ctx = _orchestrator.ActiveContext;
            bool isBuilding = _orchestrator.IsBuilding && ctx != null;

            float progress = isBuilding ? ctx.Progress : 0f;
            string centerText = isBuilding ? $"{progress * 100:F0}%" : "IDLE";
            Color ringColor = isBuilding ? NexusStyles.Accent : NexusStyles.Border;

            float ringSize = Mathf.Min(rect.width - 20f, rect.height - 50f);
            var ringRect = new Rect(rect.x + (rect.width - ringSize) * 0.5f,
                rect.y + 28f, ringSize, ringSize);

            ChartRenderer.DrawProgressRing(ringRect, progress, ringColor, centerText, thickness: 10f);

            if (isBuilding)
            {
                GUI.Label(new Rect(rect.x + 4f, ringRect.yMax + 4f, rect.width - 8f, 16f),
                    ctx.CurrentStepName,
                    new GUIStyle(NexusStyles.LabelSecondary) { alignment = TextAnchor.MiddleCenter });

                float elapsed = (float)(System.DateTime.UtcNow - ctx.StartTime).TotalSeconds;
                GUI.Label(new Rect(rect.x + 4f, ringRect.yMax + 20f, rect.width - 8f, 16f),
                    $"Elapsed: {FormatDuration(elapsed)}",
                    new GUIStyle(NexusStyles.LabelSecondary) { alignment = TextAnchor.MiddleCenter });
            }
        }

        private void DrawStepBreakdown(Rect rect)
        {
            var steps = _orchestrator.Profiler.GetLastSessionStepBreakdown();
            if (steps.Count == 0) { DrawEmpty(rect, "Step Breakdown"); return; }

            var vals = new float[steps.Count];
            var labels = new string[steps.Count];
            var colors = new Color[steps.Count];
            Color[] palette = { NexusStyles.Accent, NexusStyles.AccentHot, NexusStyles.Success,
                NexusStyles.Warning, NexusStyles.Error };
            int i = 0;
            foreach (var kv in steps)
            {
                vals[i] = kv.Value;
                labels[i] = kv.Key.Length > 10 ? kv.Key[..10] + "…" : kv.Key;
                colors[i] = palette[i % palette.Length];
                i++;
            }
            ChartRenderer.DrawBarChart(rect, vals, colors, labels, "Step Breakdown (s)");
        }

        private void DrawSessionTrend(Rect rect)
        {
            var trend = _orchestrator.Profiler.GetBuildTimeTrend(15);
            if (trend.Length < 2) { DrawEmpty(rect, "Session Trend"); return; }

            ChartRenderer.DrawLineChart(rect, trend,
                NexusStyles.AccentHot,
                new Color(NexusStyles.AccentHot.r, NexusStyles.AccentHot.g, NexusStyles.AccentHot.b, 0.10f),
                "Build Time Trend (s)");
        }

        private static void DrawEmpty(Rect rect, string title)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width, 16f), title, NexusStyles.SectionTitle);
            GUI.Label(rect, "Run a build to see data",
                new GUIStyle(NexusStyles.LabelSecondary) { alignment = TextAnchor.MiddleCenter });
        }

        private static string FormatDuration(float s) =>
            s >= 60 ? $"{(int)(s / 60)}m {(int)(s % 60)}s" : $"{s:F1}s";
        #endregion
    }
}
#endif
