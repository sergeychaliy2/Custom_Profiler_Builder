#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NexusBuildPro.Editor.Core;
using NexusBuildPro.Editor.Strategies;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.UI.Views
{
    /// <summary>
    /// Shows all registered platform strategies as selectable tiles.
    /// Displays module installation status and platform-specific info.
    /// </summary>
    public sealed class PlatformConfigView
    {
        #region Fields
        private readonly List<IPlatformBuildStrategy> _strategies;
        private int _selectedIndex = -1;
        private Vector2 _scroll;
        #endregion

        #region Events
        public event Action<IPlatformBuildStrategy> OnPlatformSelected;
        #endregion

        #region Constructor
        public PlatformConfigView(List<IPlatformBuildStrategy> strategies)
        {
            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
        }
        #endregion

        #region Public Methods
        public void Draw(Rect rect)
        {
            float tileW = 150f;
            float tileH = 90f;
            float padding = 10f;

            GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width, 20),
                "SELECT BUILD PLATFORM", NexusStyles.SectionTitle);

            // Tile grid
            float gridY = rect.y + 32f;
            float gridH = rect.height - 36f;
            _scroll = GUI.BeginScrollView(new Rect(rect.x, gridY, rect.width, gridH), _scroll,
                new Rect(0, 0, rect.width - 16f, ComputeGridHeight(tileW, tileH, padding, rect.width)));

            int cols = Mathf.Max(1, (int)((rect.width - padding) / (tileW + padding)));
            int row = 0, col = 0;

            for (int i = 0; i < _strategies.Count; i++)
            {
                var strategy = _strategies[i];
                float tx = padding + col * (tileW + padding);
                float ty = padding + row * (tileH + padding);
                DrawPlatformTile(new Rect(tx, ty, tileW, tileH), strategy, i, i == _selectedIndex);

                col++;
                if (col >= cols) { col = 0; row++; }
            }

            GUI.EndScrollView();
        }

        public IPlatformBuildStrategy GetSelectedStrategy() =>
            _selectedIndex >= 0 && _selectedIndex < _strategies.Count
                ? _strategies[_selectedIndex]
                : null;

        public void SelectStrategy(IPlatformBuildStrategy strategy)
        {
            _selectedIndex = _strategies.IndexOf(strategy);
        }
        #endregion

        #region Private Methods
        private void DrawPlatformTile(Rect rect, IPlatformBuildStrategy strategy, int index, bool isSelected)
        {
            var borderColor = isSelected ? NexusStyles.Accent :
                              strategy.IsModuleInstalled ? NexusStyles.Border :
                              new Color(0.4f, 0.2f, 0.2f, 1f);

            NexusStyles.DrawRect(rect, isSelected ? NexusStyles.SurfaceAlt : NexusStyles.Surface);
            NexusStyles.DrawBorder(rect, borderColor, isSelected ? 2f : 1f);

            // Platform color bar on left
            NexusStyles.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), strategy.PlatformColor);

            // Platform name
            GUI.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 14f, 20f),
                strategy.PlatformName,
                new GUIStyle(NexusStyles.LabelBold) { fontSize = isSelected ? 13 : 12 });

            // Target label
            GUI.Label(new Rect(rect.x + 10f, rect.y + 30f, rect.width - 14f, 16f),
                strategy.Target.ToString(),
                NexusStyles.LabelSecondary);

            // Module status
            var statusColor = strategy.IsModuleInstalled ? NexusStyles.Success : NexusStyles.Error;
            var statusText = strategy.IsModuleInstalled ? "● Module installed" : "✗ Module missing";
            GUI.Label(new Rect(rect.x + 10f, rect.y + 48f, rect.width - 14f, 14f),
                statusText, new GUIStyle(NexusStyles.LabelSecondary)
                { normal = { textColor = statusColor }, fontSize = 9 });

            // Platform ID badge
            NexusStyles.DrawRect(new Rect(rect.x + 10f, rect.y + 66f, 60f, 14f),
                new Color(strategy.PlatformColor.r, strategy.PlatformColor.g, strategy.PlatformColor.b, 0.25f));
            GUI.Label(new Rect(rect.x + 10f, rect.y + 66f, 60f, 14f),
                strategy.PlatformId.ToUpper(),
                new GUIStyle(NexusStyles.LabelSecondary)
                { alignment = TextAnchor.MiddleCenter, fontSize = 8 });

            // Click
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                _selectedIndex = index;
                OnPlatformSelected?.Invoke(strategy);
            }

            // Hover tint
            if (rect.Contains(Event.current.mousePosition) && !isSelected)
            {
                NexusStyles.DrawRect(rect, new Color(1f, 1f, 1f, 0.03f));
                HandleUtility.Repaint();
            }
        }

        private float ComputeGridHeight(float tileW, float tileH, float padding, float totalW)
        {
            int cols = Mathf.Max(1, (int)((totalW - padding) / (tileW + padding)));
            int rows = Mathf.CeilToInt((float)_strategies.Count / cols);
            return padding + rows * (tileH + padding);
        }
        #endregion
    }
}
#endif
