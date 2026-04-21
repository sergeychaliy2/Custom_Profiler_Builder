#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.UI
{
    /// <summary>
    /// GPU-accelerated chart rendering using GL primitives.
    /// Supports line charts, bar charts, pie charts, area charts, and timeline/Gantt views.
    /// All methods are immediate-mode — call inside OnGUI.
    /// </summary>
    [InitializeOnLoad]
    public static class ChartRenderer
    {
        #region Materials
        private static Material _glMaterial;

        // Ensures the GL material is released before a domain reload, otherwise Unity
        // logs "Cleaning up leaked objects in scene since no game object, component or
        // manager is referencing them" for the Internal-Colored material.
        static ChartRenderer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeMaterial;
            EditorApplication.quitting += DisposeMaterial;
        }

        private static void DisposeMaterial()
        {
            if (_glMaterial == null) return;
            UnityEngine.Object.DestroyImmediate(_glMaterial);
            _glMaterial = null;
        }

        private static Material GLMat
        {
            get
            {
                if (_glMaterial == null)
                {
                    var shader = Shader.Find("Hidden/Internal-Colored");
                    _glMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    _glMaterial.SetInt("_ZWrite", 0);
                }
                return _glMaterial;
            }
        }
        #endregion

        #region Line Chart
        /// <summary>Renders a smooth line chart with optional area fill.</summary>
        public static void DrawLineChart(Rect rect, float[] values, Color lineColor, Color fillColor,
            string title = null, string[] xLabels = null, float yMax = 0f, bool drawGrid = true)
        {
            if (values == null || values.Length < 2) { DrawEmptyChart(rect, title); return; }

            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);

            const float padLeft = 44f, padRight = 12f, padTop = 24f, padBottom = 28f;
            var plotRect = new Rect(rect.x + padLeft, rect.y + padTop,
                rect.width - padLeft - padRight, rect.height - padTop - padBottom);

            float maxVal = yMax > 0 ? yMax : MaxOf(values);
            if (maxVal <= 0) maxVal = 1f;

            if (drawGrid) DrawGrid(plotRect, maxVal, 5);
            if (!string.IsNullOrEmpty(title)) DrawChartTitle(rect, title);
            DrawYAxisLabels(rect, plotRect, maxVal, 5);
            if (xLabels != null) DrawXAxisLabels(rect, plotRect, xLabels);

            // Area fill
            if (fillColor.a > 0)
            {
                GLMat.SetPass(0);
                GL.PushMatrix();
                GL.LoadPixelMatrix();
                GL.Begin(GL.TRIANGLES);
                for (int i = 0; i < values.Length - 1; i++)
                {
                    float x0 = plotRect.x + plotRect.width * i / (values.Length - 1);
                    float x1 = plotRect.x + plotRect.width * (i + 1) / (values.Length - 1);
                    float y0 = plotRect.yMax - plotRect.height * Mathf.Clamp01(values[i] / maxVal);
                    float y1 = plotRect.yMax - plotRect.height * Mathf.Clamp01(values[i + 1] / maxVal);
                    float yBase = plotRect.yMax;

                    GL.Color(fillColor);
                    GL.Vertex3(x0, Screen.height - y0, 0);
                    GL.Vertex3(x1, Screen.height - y1, 0);
                    GL.Vertex3(x0, Screen.height - yBase, 0);
                    GL.Vertex3(x1, Screen.height - y1, 0);
                    GL.Vertex3(x1, Screen.height - yBase, 0);
                    GL.Vertex3(x0, Screen.height - yBase, 0);
                }
                GL.End();
                GL.PopMatrix();
            }

            // Line
            DrawGLLine(values, plotRect, maxVal, lineColor, thickness: 2f);

            // Points
            for (int i = 0; i < values.Length; i++)
            {
                float x = plotRect.x + plotRect.width * i / (values.Length - 1);
                float y = plotRect.yMax - plotRect.height * Mathf.Clamp01(values[i] / maxVal);
                DrawDot(new Vector2(x, y), 4f, lineColor);
            }
        }
        #endregion

        #region Bar Chart
        /// <summary>Renders a vertical bar chart with colored bars and value labels.</summary>
        public static void DrawBarChart(Rect rect, float[] values, Color[] barColors,
            string[] labels = null, string title = null, float yMax = 0f)
        {
            if (values == null || values.Length == 0) { DrawEmptyChart(rect, title); return; }

            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);

            const float padLeft = 44f, padRight = 12f, padTop = 24f, padBottom = 32f;
            var plotRect = new Rect(rect.x + padLeft, rect.y + padTop,
                rect.width - padLeft - padRight, rect.height - padTop - padBottom);

            float maxVal = yMax > 0 ? yMax : MaxOf(values);
            if (maxVal <= 0) maxVal = 1f;

            DrawGrid(plotRect, maxVal, 4);
            if (!string.IsNullOrEmpty(title)) DrawChartTitle(rect, title);
            DrawYAxisLabels(rect, plotRect, maxVal, 4);

            float barSpacing = 0.15f;
            float totalSpacing = plotRect.width * barSpacing * (values.Length + 1);
            float barWidth = (plotRect.width - totalSpacing) / values.Length;
            if (barWidth < 4f) barWidth = 4f;

            for (int i = 0; i < values.Length; i++)
            {
                float normalized = Mathf.Clamp01(values[i] / maxVal);
                float barH = plotRect.height * normalized;
                float x = plotRect.x + plotRect.width * barSpacing * (i + 1) + barWidth * i;
                float y = plotRect.yMax - barH;

                var barRect = new Rect(x, y, barWidth, barH);
                var color = barColors != null && i < barColors.Length ? barColors[i] : NexusStyles.Accent;
                NexusStyles.DrawRect(barRect, color);

                // Value label on top
                if (values[i] > 0)
                {
                    var labelRect = new Rect(x - 4, y - 16, barWidth + 8, 14);
                    GUI.Label(labelRect, $"{values[i]:F1}", new GUIStyle(NexusStyles.LabelSecondary)
                        { alignment = TextAnchor.MiddleCenter, fontSize = 9 });
                }

                // X label
                if (labels != null && i < labels.Length)
                {
                    var lblRect = new Rect(x - 8, plotRect.yMax + 4, barWidth + 16, 18);
                    GUI.Label(lblRect, labels[i], new GUIStyle(NexusStyles.LabelSecondary)
                        { alignment = TextAnchor.MiddleCenter, fontSize = 9 });
                }
            }
        }
        #endregion

        #region Pie Chart
        /// <summary>Renders a donut/pie chart with legend.</summary>
        public static void DrawPieChart(Rect rect, float[] values, Color[] colors,
            string[] labels = null, string title = null, bool isDonut = true)
        {
            if (values == null || values.Length == 0) { DrawEmptyChart(rect, title); return; }

            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);

            if (!string.IsNullOrEmpty(title)) DrawChartTitle(rect, title);

            float total = 0;
            foreach (var v in values) total += v;
            if (total <= 0) total = 1f;

            // Split: pie on left, legend on right
            float legendW = 100f;
            var pieRect = new Rect(rect.x + 8, rect.y + 24, rect.width - legendW - 16, rect.height - 32);
            var legendRect = new Rect(rect.xMax - legendW - 4, rect.y + 28, legendW, rect.height - 32);

            float cx = pieRect.x + pieRect.width * 0.5f;
            float cy = pieRect.y + pieRect.height * 0.5f;
            float radius = Mathf.Min(pieRect.width, pieRect.height) * 0.45f;
            float innerRadius = isDonut ? radius * 0.5f : 0f;

            float angle = -90f;
            GLMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            for (int i = 0; i < values.Length; i++)
            {
                float sweep = 360f * (values[i] / total);
                var color = colors != null && i < colors.Length ? colors[i] : Color.white;
                DrawArcSegment(cx, cy, innerRadius, radius, angle, sweep, color);
                angle += sweep;
            }

            GL.PopMatrix();

            // Legend
            float ly = legendRect.y;
            for (int i = 0; i < values.Length; i++)
            {
                if (ly > legendRect.yMax - 14) break;
                var color = colors != null && i < colors.Length ? colors[i] : Color.white;
                NexusStyles.DrawRect(new Rect(legendRect.x, ly + 2, 10, 10), color);
                string lbl = labels != null && i < labels.Length ? labels[i] : $"Item {i}";
                float pct = total > 0 ? values[i] / total * 100f : 0f;
                GUI.Label(new Rect(legendRect.x + 14, ly, legendRect.width - 14, 14),
                    $"{lbl} ({pct:F0}%)", new GUIStyle(NexusStyles.LabelSecondary) { fontSize = 9 });
                ly += 16f;
            }
        }
        #endregion

        #region Timeline / Gantt
        /// <summary>Renders a horizontal Gantt-style timeline of build steps.</summary>
        public static void DrawTimeline(Rect rect, IReadOnlyDictionary<string, float> steps,
            float totalDuration, string title = null)
        {
            if (steps == null || steps.Count == 0) { DrawEmptyChart(rect, title); return; }

            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);
            if (!string.IsNullOrEmpty(title)) DrawChartTitle(rect, title);

            const float padLeft = 110f, padRight = 12f, padTop = 24f;
            float rowH = 18f;
            float plotW = rect.width - padLeft - padRight;
            float maxDur = totalDuration > 0 ? totalDuration : 1f;

            int row = 0;
            foreach (var kv in steps)
            {
                float y = rect.y + padTop + row * (rowH + 3f);
                if (y + rowH > rect.yMax - 4) break;

                // Label
                GUI.Label(new Rect(rect.x + 4, y, padLeft - 8, rowH), kv.Key,
                    new GUIStyle(NexusStyles.LabelSecondary) { alignment = TextAnchor.MiddleRight, fontSize = 9 });

                // Bar background
                var bgRect = new Rect(rect.x + padLeft, y + 2, plotW, rowH - 4);
                NexusStyles.DrawRect(bgRect, NexusStyles.Border);

                // Bar fill
                float normalizedW = Mathf.Clamp01(kv.Value / maxDur) * plotW;
                var fillColor = kv.Key.Contains("Unity") ? NexusStyles.Warning :
                                kv.Key.Contains("Optim") ? NexusStyles.Success :
                                NexusStyles.Accent;
                if (normalizedW > 1f)
                    NexusStyles.DrawRect(new Rect(rect.x + padLeft, y + 2, normalizedW, rowH - 4), fillColor);

                // Duration text
                GUI.Label(new Rect(rect.x + padLeft + normalizedW + 4, y, 60, rowH),
                    $"{kv.Value:F2}s", new GUIStyle(NexusStyles.LabelSecondary) { fontSize = 9 });

                row++;
            }
        }
        #endregion

        #region Progress Ring
        /// <summary>Draws an animated circular progress ring.</summary>
        public static void DrawProgressRing(Rect rect, float progress, Color ringColor,
            string centerText = null, float thickness = 8f)
        {
            float cx = rect.x + rect.width * 0.5f;
            float cy = rect.y + rect.height * 0.5f;
            float radius = Mathf.Min(rect.width, rect.height) * 0.4f;

            NexusStyles.DrawRect(rect, Color.clear);

            // Background ring
            GLMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            DrawRingArc(cx, cy, radius - thickness * 0.5f, 0, 360, NexusStyles.Border, thickness);
            // Progress arc
            DrawRingArc(cx, cy, radius - thickness * 0.5f, -90, 360 * Mathf.Clamp01(progress), ringColor, thickness);
            GL.PopMatrix();

            // Center text
            if (!string.IsNullOrEmpty(centerText))
            {
                GUI.Label(rect, centerText, new GUIStyle(NexusStyles.MetricValue)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                });
            }
        }
        #endregion

        #region Private Helpers
        private static void DrawGLLine(float[] values, Rect plotRect, float maxVal, Color color, float thickness)
        {
            GLMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);
            GL.Color(color);

            for (int i = 0; i < values.Length - 1; i++)
            {
                float x0 = plotRect.x + plotRect.width * i / (values.Length - 1);
                float x1 = plotRect.x + plotRect.width * (i + 1) / (values.Length - 1);
                float y0 = plotRect.yMax - plotRect.height * Mathf.Clamp01(values[i] / maxVal);
                float y1 = plotRect.yMax - plotRect.height * Mathf.Clamp01(values[i + 1] / maxVal);

                GL.Vertex3(x0, Screen.height - y0, 0);
                GL.Vertex3(x1, Screen.height - y1, 0);
            }
            GL.End();
            GL.PopMatrix();
        }

        private static void DrawDot(Vector2 center, float radius, Color color)
        {
            GLMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            int segs = 12;
            for (int i = 0; i < segs; i++)
            {
                float a0 = Mathf.PI * 2 * i / segs;
                float a1 = Mathf.PI * 2 * (i + 1) / segs;
                GL.Vertex3(center.x, Screen.height - center.y, 0);
                GL.Vertex3(center.x + Mathf.Cos(a0) * radius, Screen.height - (center.y + Mathf.Sin(a0) * radius), 0);
                GL.Vertex3(center.x + Mathf.Cos(a1) * radius, Screen.height - (center.y + Mathf.Sin(a1) * radius), 0);
            }
            GL.End();
            GL.PopMatrix();
        }

        private static void DrawArcSegment(float cx, float cy, float innerR, float outerR,
            float startDeg, float sweepDeg, Color color)
        {
            int segs = Mathf.Max(6, Mathf.CeilToInt(sweepDeg / 4f));
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            for (int i = 0; i < segs; i++)
            {
                float a0 = (startDeg + sweepDeg * i / segs) * Mathf.Deg2Rad;
                float a1 = (startDeg + sweepDeg * (i + 1) / segs) * Mathf.Deg2Rad;

                float ox0 = cx + Mathf.Cos(a0) * outerR, oy0 = cy + Mathf.Sin(a0) * outerR;
                float ox1 = cx + Mathf.Cos(a1) * outerR, oy1 = cy + Mathf.Sin(a1) * outerR;
                float ix0 = cx + Mathf.Cos(a0) * innerR, iy0 = cy + Mathf.Sin(a0) * innerR;
                float ix1 = cx + Mathf.Cos(a1) * innerR, iy1 = cy + Mathf.Sin(a1) * innerR;

                float sy = Screen.height;
                GL.Vertex3(ox0, sy - oy0, 0); GL.Vertex3(ox1, sy - oy1, 0); GL.Vertex3(ix0, sy - iy0, 0);
                GL.Vertex3(ox1, sy - oy1, 0); GL.Vertex3(ix1, sy - iy1, 0); GL.Vertex3(ix0, sy - iy0, 0);
            }
            GL.End();
        }

        private static void DrawRingArc(float cx, float cy, float r, float startDeg, float sweepDeg,
            Color color, float lineWidth)
        {
            DrawArcSegment(cx, cy, r - lineWidth * 0.5f, r + lineWidth * 0.5f, startDeg, sweepDeg, color);
        }

        private static void DrawGrid(Rect plotRect, float maxVal, int lines)
        {
            for (int i = 0; i <= lines; i++)
            {
                float y = plotRect.yMax - plotRect.height * i / lines;
                NexusStyles.DrawRect(new Rect(plotRect.x, y, plotRect.width, 1f),
                    new Color(0.25f, 0.27f, 0.35f, i == 0 ? 0.8f : 0.3f));
            }
        }

        private static void DrawChartTitle(Rect rect, string title)
        {
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, 18), title,
                new GUIStyle(NexusStyles.LabelSecondary) { fontStyle = FontStyle.Bold, fontSize = 10 });
        }

        private static void DrawYAxisLabels(Rect rect, Rect plotRect, float maxVal, int lines)
        {
            for (int i = 0; i <= lines; i++)
            {
                float y = plotRect.yMax - plotRect.height * i / lines;
                float val = maxVal * i / lines;
                string label = val >= 60 ? $"{val / 60:F0}m" : $"{val:F0}s";
                GUI.Label(new Rect(rect.x + 2, y - 7, 40, 14), label,
                    new GUIStyle(NexusStyles.LabelSecondary) { alignment = TextAnchor.MiddleRight, fontSize = 8 });
            }
        }

        private static void DrawXAxisLabels(Rect rect, Rect plotRect, string[] labels)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                float x = plotRect.x + plotRect.width * i / (labels.Length - 1);
                GUI.Label(new Rect(x - 20, plotRect.yMax + 2, 40, 14), labels[i],
                    new GUIStyle(NexusStyles.LabelSecondary) { alignment = TextAnchor.UpperCenter, fontSize = 8 });
            }
        }

        private static void DrawEmptyChart(Rect rect, string title)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, NexusStyles.Border);
            if (!string.IsNullOrEmpty(title)) DrawChartTitle(rect, title);
            GUI.Label(rect, "No data", new GUIStyle(NexusStyles.LabelSecondary)
                { alignment = TextAnchor.MiddleCenter });
        }

        private static float MaxOf(float[] values)
        {
            float max = 0;
            foreach (var v in values) if (v > max) max = v;
            return max;
        }
        #endregion
    }
}
#endif
