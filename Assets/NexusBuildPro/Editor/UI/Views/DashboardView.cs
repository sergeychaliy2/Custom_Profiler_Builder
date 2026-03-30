#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NexusBuildPro.Editor.Core;
using NexusBuildPro.Editor.Data;
using NexusBuildPro.Editor.Profiling;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.UI.Views
{
    /// <summary>
    /// Dashboard overview: key metrics cards, quick-build status,
    /// recent history list, and a live build-time trend line chart.
    /// </summary>
    public sealed class DashboardView
    {
        #region Fields
        private readonly BuildOrchestrator _orchestrator;
        private Vector2 _scroll;
        private float _animTime;
        #endregion

        #region Constructor
        public DashboardView(BuildOrchestrator orchestrator)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        }
        #endregion

        #region Public Methods
        public void Draw(Rect rect)
        {
            _animTime = (float)EditorApplication.timeSinceStartup;

            // Top metric cards row
            float cardRowH = 90f;
            DrawMetricCards(new Rect(rect.x, rect.y, rect.width, cardRowH));

            // Rest split: trend chart (left) + recent history (right)
            float contentY = rect.y + cardRowH + 8f;
            float contentH = rect.height - cardRowH - 8f;
            float chartW = rect.width * 0.58f;
            float histW = rect.width - chartW - 8f;

            DrawTrendChart(new Rect(rect.x, contentY, chartW - 4f, contentH));
            DrawRecentHistory(new Rect(rect.x + chartW + 4f, contentY, histW, contentH));
        }
        #endregion

        #region Private Methods
        private void DrawMetricCards(Rect rect)
        {
            var history = _orchestrator.GetHistory();
            var profiler = _orchestrator.Profiler;

            float cardW = (rect.width - 16f) / 4f;
            float cardSpacing = (rect.width - cardW * 4f) / 5f;

            // Build Status Card
            DrawMetricCard(
                new Rect(rect.x + cardSpacing, rect.y, cardW, rect.height - 4f),
                _orchestrator.IsBuilding ? "BUILDING" : "IDLE",
                "Status",
                _orchestrator.IsBuilding ? NexusStyles.Warning : NexusStyles.Success,
                _orchestrator.IsBuilding ? PulsingAlpha() : 1f);

            // Total Builds Card
            DrawMetricCard(
                new Rect(rect.x + cardSpacing * 2 + cardW, rect.y, cardW, rect.height - 4f),
                history.Entries.Count.ToString(),
                "Total Builds",
                NexusStyles.Accent, 1f);

            // Avg Build Time Card
            float avg = profiler.GetAverageBuildTime();
            DrawMetricCard(
                new Rect(rect.x + cardSpacing * 3 + cardW * 2, rect.y, cardW, rect.height - 4f),
                avg > 0 ? FormatDuration(avg) : "--",
                "Avg Build Time",
                NexusStyles.AccentHot, 1f);

            // Success Rate Card
            int successCount = 0;
            foreach (var e in history.Entries) if (e.Success) successCount++;
            float rate = history.Entries.Count > 0
                ? (float)successCount / history.Entries.Count * 100f : 0f;
            DrawMetricCard(
                new Rect(rect.x + cardSpacing * 4 + cardW * 3, rect.y, cardW, rect.height - 4f),
                history.Entries.Count > 0 ? $"{rate:F0}%" : "--",
                "Success Rate",
                rate >= 80f ? NexusStyles.Success : rate >= 50f ? NexusStyles.Warning : NexusStyles.Error, 1f);
        }

        private void DrawMetricCard(Rect rect, string value, string label, Color accentColor, float alpha)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, accentColor, 1.5f);

            // Accent top bar
            NexusStyles.DrawRect(new Rect(rect.x, rect.y, rect.width, 3f), accentColor);

            // Value
            GUI.Label(new Rect(rect.x, rect.y + 12f, rect.width, 36f), value,
                new GUIStyle(NexusStyles.MetricValue) { normal = { textColor = accentColor } });

            // Label
            GUI.Label(new Rect(rect.x, rect.yMax - 22f, rect.width, 18f), label, NexusStyles.MetricLabel);

            GUI.color = prevColor;
        }

        private void DrawTrendChart(Rect rect)
        {
            var trend = _orchestrator.Profiler.GetBuildTimeTrend(20);
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);

            if (trend.Length < 2)
            {
                GUI.Label(rect, "Run at least 2 builds to see trends",
                    new GUIStyle(NexusStyles.LabelSecondary) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            // Build xLabels from history
            var history = _orchestrator.GetHistory();
            var labels = new string[trend.Length];
            int startIdx = history.Entries.Count - trend.Length;
            for (int i = 0; i < trend.Length; i++)
            {
                int idx = startIdx + i;
                labels[i] = idx >= 0 && idx < history.Entries.Count
                    ? history.Entries[idx].ParsedTime.ToString("HH:mm")
                    : "";
            }

            ChartRenderer.DrawLineChart(rect, trend,
                NexusStyles.Accent,
                new Color(NexusStyles.Accent.r, NexusStyles.Accent.g, NexusStyles.Accent.b, 0.12f),
                "Build Time Trend (seconds)",
                labels,
                yMax: 0f,
                drawGrid: true);
        }

        private void DrawRecentHistory(Rect rect)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);

            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width, 16f),
                "RECENT BUILDS", NexusStyles.SectionTitle);

            var history = _orchestrator.GetHistory();
            if (history.Entries.Count == 0)
            {
                GUI.Label(new Rect(rect.x, rect.y + 36f, rect.width, 30f),
                    "No builds yet", new GUIStyle(NexusStyles.LabelSecondary) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            float rowY = rect.y + 28f;
            float rowH = 36f;
            int maxRows = Mathf.FloorToInt((rect.height - 32f) / rowH);
            int count = Mathf.Min(history.Entries.Count, maxRows);

            for (int i = 0; i < count; i++)
            {
                var entry = history.Entries[i];
                var rowRect = new Rect(rect.x + 4f, rowY, rect.width - 8f, rowH - 2f);

                NexusStyles.DrawRect(rowRect, i % 2 == 0 ? NexusStyles.SurfaceAlt : NexusStyles.Surface);

                // Status dot
                var dotColor = entry.Success ? NexusStyles.Success : NexusStyles.Error;
                NexusStyles.DrawRect(new Rect(rowRect.x + 6f, rowRect.y + 12f, 8f, 8f), dotColor);

                // Platform
                GUI.Label(new Rect(rowRect.x + 20f, rowRect.y + 2f, 80f, 16f),
                    entry.PlatformId, NexusStyles.LabelBold);

                // Time
                GUI.Label(new Rect(rowRect.x + 20f, rowRect.y + 18f, 80f, 14f),
                    FormatDuration(entry.DurationSeconds), NexusStyles.LabelSecondary);

                // Size
                GUI.Label(new Rect(rowRect.x + 100f, rowRect.y + 2f, 70f, 16f),
                    entry.FormatSize(), NexusStyles.LabelSecondary);

                // Timestamp
                GUI.Label(new Rect(rowRect.x + 100f, rowRect.y + 18f, 80f, 14f),
                    entry.ParsedTime.ToString("MM/dd HH:mm"), NexusStyles.LabelSecondary);

                rowY += rowH;
            }
        }

        private float PulsingAlpha() => 0.6f + 0.4f * Mathf.Sin(_animTime * 3f);

        private static string FormatDuration(float seconds)
        {
            if (seconds >= 60f) return $"{(int)(seconds / 60)}m {(int)(seconds % 60)}s";
            return $"{seconds:F1}s";
        }
        #endregion
    }
}
#endif
