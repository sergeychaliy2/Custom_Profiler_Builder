#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NexusBuildPro.Editor.Core;
using NexusBuildPro.Editor.Data;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.UI.Views
{
    /// <summary>
    /// Full build history table with search/filter, sortable columns,
    /// bar chart of build durations, and pie chart of platform distribution.
    /// </summary>
    public sealed class BuildHistoryView
    {
        #region Fields
        private readonly BuildOrchestrator _orchestrator;
        private Vector2 _tableScroll;
        private string _filterText = "";
        private bool _showOnlyFailed;
        private int _sortColumn = 0;
        private bool _sortAscending;
        private readonly List<BuildHistoryEntry> _filtered = new();
        #endregion

        #region Constructor
        public BuildHistoryView(BuildOrchestrator orchestrator)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        }
        #endregion

        #region Public Methods
        public void Draw(Rect rect)
        {
            float toolbarH = 32f;
            float chartH = 160f;
            float chartW = rect.width * 0.5f - 4f;

            // Toolbar
            DrawToolbar(new Rect(rect.x, rect.y, rect.width, toolbarH));

            // Charts row
            float chartsY = rect.y + toolbarH + 4f;
            DrawDurationBarChart(new Rect(rect.x, chartsY, chartW, chartH));
            DrawPlatformPieChart(new Rect(rect.x + chartW + 8f, chartsY, chartW, chartH));

            // Table
            float tableY = chartsY + chartH + 8f;
            float tableH = rect.height - toolbarH - chartH - 20f;
            DrawHistoryTable(new Rect(rect.x, tableY, rect.width, tableH));
        }
        #endregion

        #region Private Methods
        private void DrawToolbar(Rect rect)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);

            // Search
            GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, 50f, 18f), "Filter:", NexusStyles.LabelSecondary);
            _filterText = GUI.TextField(new Rect(rect.x + 56f, rect.y + 7f, 180f, 20f),
                _filterText, NexusStyles.TextField);

            // Failed toggle
            _showOnlyFailed = GUI.Toggle(
                new Rect(rect.x + 248f, rect.y + 8f, 100f, 18f),
                _showOnlyFailed, " Failures only", NexusStyles.LabelSecondary);

            // Clear button
            if (GUI.Button(new Rect(rect.xMax - 100f, rect.y + 6f, 90f, 22f), "Clear History",
                NexusStyles.SmallButton))
            {
                if (EditorUtility.DisplayDialog("Clear History",
                    "Clear all build history entries?", "Clear", "Cancel"))
                {
                    _orchestrator.GetHistory().Entries.Clear();
                    _orchestrator.GetHistory().Save();
                }
            }
        }

        private void DrawDurationBarChart(Rect rect)
        {
            var entries = GetFilteredEntries();
            int count = Mathf.Min(entries.Count, 12);
            if (count == 0) { DrawEmptyBox(rect, "Build Duration History"); return; }

            var values = new float[count];
            var labels = new string[count];
            var colors = new Color[count];

            for (int i = 0; i < count; i++)
            {
                var e = entries[entries.Count - count + i];
                values[i] = e.DurationSeconds;
                labels[i] = e.ParsedTime.ToString("HH:mm");
                colors[i] = e.Success ? NexusStyles.Accent : NexusStyles.Error;
            }

            ChartRenderer.DrawBarChart(rect, values, colors, labels, "Build Duration (s)");
        }

        private void DrawPlatformPieChart(Rect rect)
        {
            var entries = _orchestrator.GetHistory().Entries;
            if (entries.Count == 0) { DrawEmptyBox(rect, "Platform Distribution"); return; }

            var counts = new Dictionary<string, float>();
            foreach (var e in entries)
            {
                if (!counts.ContainsKey(e.PlatformId)) counts[e.PlatformId] = 0;
                counts[e.PlatformId]++;
            }

            var values = new float[counts.Count];
            var labels = new string[counts.Count];
            var colors = new Color[counts.Count];
            Color[] palette = {
                NexusStyles.Accent, NexusStyles.AccentHot, NexusStyles.Success,
                NexusStyles.Warning, NexusStyles.Error,
                new Color(0.8f, 0.4f, 1f), new Color(1f, 0.6f, 0.2f)
            };

            int idx = 0;
            foreach (var kv in counts)
            {
                values[idx] = kv.Value;
                labels[idx] = kv.Key;
                colors[idx] = palette[idx % palette.Length];
                idx++;
            }

            ChartRenderer.DrawPieChart(rect, values, colors, labels, "Platform Distribution", isDonut: true);
        }

        private void DrawHistoryTable(Rect rect)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);

            // Column headers
            float headerH = 22f;
            var headerRect = new Rect(rect.x, rect.y, rect.width, headerH);
            NexusStyles.DrawRect(headerRect, NexusStyles.SurfaceAlt);

            string[] colNames = { "#", "Status", "Platform", "Duration", "Size", "Version", "Time" };
            float[] colWidths = { 32f, 60f, 100f, 80f, 80f, 70f, 110f };
            float x = rect.x + 4f;
            for (int c = 0; c < colNames.Length; c++)
            {
                bool isActiveSort = _sortColumn == c;
                string label = isActiveSort
                    ? $"{colNames[c]} {(_sortAscending ? "▲" : "▼")}"
                    : colNames[c];
                if (GUI.Button(new Rect(x, rect.y + 2f, colWidths[c], 18f), label, NexusStyles.SmallButton))
                {
                    if (_sortColumn == c) _sortAscending = !_sortAscending;
                    else { _sortColumn = c; _sortAscending = true; }
                }
                x += colWidths[c] + 4f;
            }

            // Rows
            var entries = GetSortedEntries();
            float rowH = 22f;
            float contentH = entries.Count * rowH;
            _tableScroll = GUI.BeginScrollView(
                new Rect(rect.x, rect.y + headerH, rect.width, rect.height - headerH),
                _tableScroll,
                new Rect(0, 0, rect.width - 16f, Mathf.Max(contentH, rect.height - headerH)));

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                float ry = i * rowH;
                var rowRect = new Rect(0, ry, rect.width - 16f, rowH - 1f);

                NexusStyles.DrawRect(rowRect, i % 2 == 0 ? NexusStyles.Surface : NexusStyles.SurfaceAlt);

                var statusColor = e.Success ? NexusStyles.Success : NexusStyles.Error;
                x = 4f;

                // Index
                GUI.Label(new Rect(x, ry + 4f, colWidths[0], 14f),
                    (i + 1).ToString(), NexusStyles.LabelSecondary); x += colWidths[0] + 4f;

                // Status
                NexusStyles.DrawRect(new Rect(x + 4f, ry + 7f, 10f, 10f), statusColor);
                GUI.Label(new Rect(x + 18f, ry + 4f, 40f, 14f),
                    e.Success ? "OK" : "FAIL",
                    new GUIStyle(NexusStyles.LabelSecondary) { normal = { textColor = statusColor } });
                x += colWidths[1] + 4f;

                GUI.Label(new Rect(x, ry + 4f, colWidths[2], 14f), e.PlatformId, NexusStyles.Label); x += colWidths[2] + 4f;
                GUI.Label(new Rect(x, ry + 4f, colWidths[3], 14f), FormatDuration(e.DurationSeconds), NexusStyles.LabelSecondary); x += colWidths[3] + 4f;
                GUI.Label(new Rect(x, ry + 4f, colWidths[4], 14f), e.FormatSize(), NexusStyles.LabelSecondary); x += colWidths[4] + 4f;
                GUI.Label(new Rect(x, ry + 4f, colWidths[5], 14f), e.Version, NexusStyles.LabelSecondary); x += colWidths[5] + 4f;
                GUI.Label(new Rect(x, ry + 4f, colWidths[6], 14f),
                    e.ParsedTime.ToString("yyyy-MM-dd HH:mm"), NexusStyles.LabelSecondary);
            }

            GUI.EndScrollView();
        }

        private List<BuildHistoryEntry> GetFilteredEntries()
        {
            _filtered.Clear();
            foreach (var e in _orchestrator.GetHistory().Entries)
            {
                if (_showOnlyFailed && e.Success) continue;
                if (!string.IsNullOrEmpty(_filterText) &&
                    !e.PlatformId.Contains(_filterText, StringComparison.OrdinalIgnoreCase) &&
                    !e.ProfileName.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                    continue;
                _filtered.Add(e);
            }
            return _filtered;
        }

        private List<BuildHistoryEntry> GetSortedEntries()
        {
            var list = new List<BuildHistoryEntry>(GetFilteredEntries());
            list.Sort((a, b) =>
            {
                int cmp = _sortColumn switch
                {
                    1 => a.Success.CompareTo(b.Success),
                    2 => string.Compare(a.PlatformId, b.PlatformId, StringComparison.Ordinal),
                    3 => a.DurationSeconds.CompareTo(b.DurationSeconds),
                    4 => a.OutputSizeBytes.CompareTo(b.OutputSizeBytes),
                    5 => string.Compare(a.Version, b.Version, StringComparison.Ordinal),
                    6 => a.ParsedTime.CompareTo(b.ParsedTime),
                    _ => 0
                };
                return _sortAscending ? cmp : -cmp;
            });
            return list;
        }

        private static void DrawEmptyBox(Rect rect, string title)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width, 16f), title, NexusStyles.SectionTitle);
            GUI.Label(rect, "No data", new GUIStyle(NexusStyles.LabelSecondary) { alignment = TextAnchor.MiddleCenter });
        }

        private static string FormatDuration(float s) =>
            s >= 60 ? $"{(int)(s / 60)}m {(int)(s % 60)}s" : $"{s:F1}s";
        #endregion
    }
}
#endif
