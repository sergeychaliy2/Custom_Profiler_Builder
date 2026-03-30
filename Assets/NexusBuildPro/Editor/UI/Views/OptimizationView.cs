#if UNITY_EDITOR
using System.Collections.Generic;
using NexusBuildPro.Editor.Cache;
using NexusBuildPro.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.UI.Views
{
    /// <summary>
    /// Shows optimization recommendations, cache status, asset stats,
    /// and allows manual cache invalidation.
    /// </summary>
    public sealed class OptimizationView
    {
        #region Fields
        private readonly BuildOrchestrator _orchestrator;
        private Vector2 _scroll;
        private List<string> _changedAssets;
        private bool _assetScanDone;
        private float _scanProgress;
        private bool _isScanning;
        #endregion

        #region Constructor
        public OptimizationView(BuildOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }
        #endregion

        #region Public Methods
        public void Draw(Rect rect)
        {
            float leftW = rect.width * 0.5f - 4f;
            float rightW = rect.width - leftW - 8f;

            DrawCachePanel(new Rect(rect.x, rect.y, leftW, rect.height * 0.5f - 4f));
            DrawRecommendationsPanel(new Rect(rect.x, rect.y + rect.height * 0.5f + 4f, leftW, rect.height * 0.5f - 4f));
            DrawOptimizationStepsPanel(new Rect(rect.x + leftW + 8f, rect.y, rightW, rect.height));
        }
        #endregion

        #region Private Methods
        private void DrawCachePanel(Rect rect)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width, 16f), "BUILD CACHE", NexusStyles.SectionTitle);

            var cache = _orchestrator.GetCache();
            float y = rect.y + 28f;

            DrawStatRow(rect.x + 8f, y, rect.width - 16f, "Cached Assets", cache.CachedAssetCount.ToString()); y += 22f;
            DrawStatRow(rect.x + 8f, y, rect.width - 16f, "Cache Size",
                FormatBytes(cache.GetCacheSizeBytes())); y += 22f;
            DrawStatRow(rect.x + 8f, y, rect.width - 16f, "Cache Dir",
                cache.CacheDirectoryPath); y += 28f;

            if (_changedAssets != null)
            {
                DrawStatRow(rect.x + 8f, y, rect.width - 16f, "Changed Since Last Build",
                    _changedAssets.Count.ToString(),
                    _changedAssets.Count > 0 ? NexusStyles.Warning : NexusStyles.Success);
                y += 22f;
            }

            if (_isScanning)
            {
                var progressRect = new Rect(rect.x + 8f, y, rect.width - 60f, 16f);
                NexusStyles.DrawRect(progressRect, NexusStyles.Border);
                NexusStyles.DrawRect(new Rect(progressRect.x, progressRect.y,
                    progressRect.width * _scanProgress, progressRect.height), NexusStyles.Accent);
                GUI.Label(new Rect(progressRect.xMax + 4f, y, 50f, 16f),
                    $"{_scanProgress * 100:F0}%", NexusStyles.LabelSecondary);
                y += 24f;
            }

            // Buttons
            float btnW = (rect.width - 28f) / 2f;
            if (GUI.Button(new Rect(rect.x + 8f, rect.yMax - 30f, btnW, 24f),
                "Scan Assets", NexusStyles.SmallButton) && !_isScanning)
            {
                ScanAssets(cache);
            }

            if (GUI.Button(new Rect(rect.x + 12f + btnW, rect.yMax - 30f, btnW, 24f),
                "Clear Cache", NexusStyles.CancelButton))
            {
                if (EditorUtility.DisplayDialog("Clear Cache",
                    "Clear the entire build cache? This will force a full rebuild.", "Clear", "Cancel"))
                {
                    cache.ClearCache();
                    _changedAssets = null;
                    _assetScanDone = false;
                }
            }
        }

        private void DrawRecommendationsPanel(Rect rect)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width, 16f),
                "RECOMMENDATIONS", NexusStyles.SectionTitle);

            float y = rect.y + 28f;
            var recs = GetRecommendations();
            foreach (var rec in recs)
            {
                if (y + 30f > rect.yMax) break;
                var rowRect = new Rect(rect.x + 6f, y, rect.width - 12f, 26f);
                NexusStyles.DrawRect(rowRect, NexusStyles.SurfaceAlt);
                NexusStyles.DrawRect(new Rect(rowRect.x, rowRect.y, 3f, rowRect.height), rec.Color);
                GUI.Label(new Rect(rowRect.x + 8f, rowRect.y + 2f, rowRect.width - 12f, 14f),
                    rec.Title, NexusStyles.LabelBold);
                GUI.Label(new Rect(rowRect.x + 8f, rowRect.y + 14f, rowRect.width - 12f, 12f),
                    rec.Detail, new GUIStyle(NexusStyles.LabelSecondary) { fontSize = 9 });
                y += 30f;
            }
        }

        private void DrawOptimizationStepsPanel(Rect rect)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width, 16f),
                "OPTIMIZATION PIPELINE", NexusStyles.SectionTitle);

            var steps = new[]
            {
                ("Texture Compression", "Enforce max size, enable mipmaps", NexusStyles.Accent),
                ("Audio Quality Reduction", "Set quality to 0.65 for non-critical clips", NexusStyles.Success),
                ("Mesh Compression", "Apply Low mesh compression to all models", NexusStyles.AccentHot),
                ("Code Stripping", "Remove unused managed code (IL2CPP)", NexusStyles.Warning),
                ("Scene Validation", "Verify all build scenes exist on disk", NexusStyles.Accent),
                ("Post-Build Summary", "Write build_summary.txt to output dir", NexusStyles.LabelSecondary.normal.textColor),
            };

            float y = rect.y + 28f;
            float prevX = rect.x + rect.width * 0.5f;
            bool prevConnected = false;

            for (int i = 0; i < steps.Length; i++)
            {
                var (name, desc, color) = steps[i];
                float stepH = 42f;
                var stepRect = new Rect(rect.x + 8f, y, rect.width - 16f, stepH);

                // Connector line
                if (i > 0)
                {
                    NexusStyles.DrawRect(new Rect(prevX - 1f, y - 8f, 2f, 8f), NexusStyles.Border);
                }

                NexusStyles.DrawRect(stepRect, NexusStyles.SurfaceAlt);
                NexusStyles.DrawRect(new Rect(stepRect.x, stepRect.y, 3f, stepH), color);
                NexusStyles.DrawBorder(stepRect, NexusStyles.Border);

                // Step number badge
                var badgeRect = new Rect(stepRect.x + 8f, stepRect.y + 12f, 20f, 20f);
                NexusStyles.DrawRect(badgeRect, color);
                GUI.Label(badgeRect, (i + 1).ToString(),
                    new GUIStyle(NexusStyles.LabelBold) { alignment = TextAnchor.MiddleCenter, fontSize = 9 });

                GUI.Label(new Rect(stepRect.x + 34f, stepRect.y + 6f, stepRect.width - 40f, 16f),
                    name, NexusStyles.LabelBold);
                GUI.Label(new Rect(stepRect.x + 34f, stepRect.y + 22f, stepRect.width - 40f, 14f),
                    desc, new GUIStyle(NexusStyles.LabelSecondary) { fontSize = 9 });

                prevX = stepRect.x + stepRect.width * 0.5f;
                y += stepH + 8f;
                if (y + stepH > rect.yMax) break;
            }
        }

        private void ScanAssets(BuildCacheManager cache)
        {
            cache.BeginSession();
            _changedAssets = cache.GetChangedAssets();
            _assetScanDone = true;
        }

        private List<(string Title, string Detail, Color Color)> GetRecommendations()
        {
            var list = new List<(string, string, Color)>();
            var guids = AssetDatabase.FindAssets("t:Texture2D");
            if (guids.Length > 100)
                list.Add(("Large Texture Count", $"{guids.Length} textures found. Enable atlasing.", NexusStyles.Warning));

            var audioGuids = AssetDatabase.FindAssets("t:AudioClip");
            if (audioGuids.Length > 50)
                list.Add(("Many Audio Clips", $"{audioGuids.Length} clips. Consider audio mixing.", NexusStyles.Warning));

            if (list.Count == 0)
                list.Add(("All Clear", "No major optimization issues detected.", NexusStyles.Success));

            return list;
        }

        private void DrawStatRow(float x, float y, float w, string label, string value,
            Color? valueColor = null)
        {
            GUI.Label(new Rect(x, y, w * 0.55f, 18f), label, NexusStyles.LabelSecondary);
            var style = new GUIStyle(NexusStyles.LabelBold);
            if (valueColor.HasValue) style.normal.textColor = valueColor.Value;
            GUI.Label(new Rect(x + w * 0.55f, y, w * 0.45f, 18f), value, style);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
            return $"{v:F1} {units[i]}";
        }
        #endregion
    }
}
#endif
