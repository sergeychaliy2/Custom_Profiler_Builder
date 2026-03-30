#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.UI
{
    /// <summary>
    /// Centralized GUIStyle cache for NexusBuildPro's editor UI.
    /// All styles are lazily initialized and cached for performance.
    /// </summary>
    public static class NexusStyles
    {
        #region Colors
        public static readonly Color Background    = new Color(0.11f, 0.11f, 0.16f, 1f);
        public static readonly Color Surface       = new Color(0.14f, 0.14f, 0.20f, 1f);
        public static readonly Color SurfaceAlt    = new Color(0.17f, 0.17f, 0.25f, 1f);
        public static readonly Color Accent        = new Color(0.28f, 0.60f, 1.00f, 1f);  // Neon blue
        public static readonly Color AccentHot     = new Color(0.10f, 0.85f, 0.75f, 1f);  // Cyan
        public static readonly Color Success       = new Color(0.20f, 0.90f, 0.50f, 1f);
        public static readonly Color Warning       = new Color(1.00f, 0.80f, 0.10f, 1f);
        public static readonly Color Error         = new Color(1.00f, 0.30f, 0.30f, 1f);
        public static readonly Color TextPrimary   = new Color(0.92f, 0.92f, 0.95f, 1f);
        public static readonly Color TextSecondary = new Color(0.55f, 0.58f, 0.65f, 1f);
        public static readonly Color TextDisabled  = new Color(0.35f, 0.37f, 0.42f, 1f);
        public static readonly Color Border        = new Color(0.25f, 0.27f, 0.35f, 1f);
        public static readonly Color Separator     = new Color(0.20f, 0.22f, 0.30f, 1f);
        #endregion

        #region Cached Textures
        private static Texture2D _solidWhite;
        private static Texture2D _roundRect;

        public static Texture2D SolidWhite
        {
            get
            {
                if (_solidWhite == null)
                {
                    _solidWhite = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                    _solidWhite.SetPixel(0, 0, Color.white);
                    _solidWhite.Apply();
                }
                return _solidWhite;
            }
        }
        #endregion

        #region Style Properties
        private static GUIStyle _windowBackground;
        private static GUIStyle _header;
        private static GUIStyle _subHeader;
        private static GUIStyle _label;
        private static GUIStyle _labelSecondary;
        private static GUIStyle _labelBold;
        private static GUIStyle _card;
        private static GUIStyle _cardAlt;
        private static GUIStyle _buildButton;
        private static GUIStyle _buildButtonRunning;
        private static GUIStyle _cancelButton;
        private static GUIStyle _tab;
        private static GUIStyle _tabActive;
        private static GUIStyle _platformBadge;
        private static GUIStyle _logInfo;
        private static GUIStyle _logWarning;
        private static GUIStyle _logError;
        private static GUIStyle _logSuccess;
        private static GUIStyle _progressBar;
        private static GUIStyle _progressBarFill;
        private static GUIStyle _sectionTitle;
        private static GUIStyle _badge;
        private static GUIStyle _toolbarBtn;
        private static GUIStyle _toolbarBtnActive;
        private static GUIStyle _metricValue;
        private static GUIStyle _metricLabel;
        private static GUIStyle _divider;
        private static GUIStyle _scrollView;
        private static GUIStyle _textField;
        private static GUIStyle _smallButton;

        public static GUIStyle WindowBackground => _windowBackground ??= CreateStyle(
            normal: Background, padding: new RectOffset(0, 0, 0, 0));

        public static GUIStyle Header => _header ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = TextPrimary },
            padding = new RectOffset(0, 0, 4, 4)
        };

        public static GUIStyle SubHeader => _subHeader ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Accent },
            padding = new RectOffset(0, 0, 2, 2)
        };

        public static GUIStyle Label => _label ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = TextPrimary },
            wordWrap = true
        };

        public static GUIStyle LabelSecondary => _labelSecondary ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = TextSecondary }
        };

        public static GUIStyle LabelBold => _labelBold ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = TextPrimary }
        };

        public static GUIStyle Card => _card ??= CreateStyle(
            normal: Surface, padding: new RectOffset(12, 12, 10, 10));

        public static GUIStyle CardAlt => _cardAlt ??= CreateStyle(
            normal: SurfaceAlt, padding: new RectOffset(12, 12, 10, 10));

        public static GUIStyle BuildButton => _buildButton ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(Accent) },
            hover = { textColor = Color.white, background = MakeTex(AccentHot) },
            active = { textColor = Color.white, background = MakeTex(AccentHot * 0.8f) },
            padding = new RectOffset(16, 16, 10, 10),
            border = new RectOffset(4, 4, 4, 4)
        };

        public static GUIStyle BuildButtonRunning => _buildButtonRunning ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.7f, 0.4f, 0.0f)) },
            hover = { textColor = Color.white, background = MakeTex(new Color(0.8f, 0.5f, 0.0f)) },
            padding = new RectOffset(16, 16, 10, 10)
        };

        public static GUIStyle CancelButton => _cancelButton ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(Error) },
            hover = { textColor = Color.white, background = MakeTex(Error * 1.2f) },
            padding = new RectOffset(12, 12, 6, 6)
        };

        public static GUIStyle Tab => _tab ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            fontStyle = FontStyle.Normal,
            normal = { textColor = TextSecondary, background = MakeTex(Surface) },
            hover = { textColor = TextPrimary, background = MakeTex(SurfaceAlt) },
            padding = new RectOffset(14, 14, 7, 7),
            border = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(1, 1, 0, 0)
        };

        public static GUIStyle TabActive => _tabActive ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(Accent) },
            hover = { textColor = Color.white, background = MakeTex(AccentHot) },
            padding = new RectOffset(14, 14, 7, 7),
            border = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(1, 1, 0, 0)
        };

        public static GUIStyle SectionTitle => _sectionTitle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            normal = { textColor = TextSecondary },
            padding = new RectOffset(0, 0, 8, 4)
        };

        public static GUIStyle MetricValue => _metricValue ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = AccentHot },
            alignment = TextAnchor.MiddleCenter
        };

        public static GUIStyle MetricLabel => _metricLabel ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = TextSecondary },
            alignment = TextAnchor.MiddleCenter
        };

        public static GUIStyle LogInfo => _logInfo ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = TextPrimary },
            padding = new RectOffset(4, 4, 1, 1),
            richText = true
        };

        public static GUIStyle LogWarning => _logWarning ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = Warning },
            padding = new RectOffset(4, 4, 1, 1),
            richText = true
        };

        public static GUIStyle LogError => _logError ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = Error },
            padding = new RectOffset(4, 4, 1, 1),
            richText = true
        };

        public static GUIStyle LogSuccess => _logSuccess ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = Success },
            padding = new RectOffset(4, 4, 1, 1),
            richText = true
        };

        public static GUIStyle SmallButton => _smallButton ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            padding = new RectOffset(8, 8, 4, 4),
            normal = { textColor = TextPrimary }
        };

        public static GUIStyle TextField => _textField ??= new GUIStyle(GUI.skin.textField)
        {
            fontSize = 11,
            normal = { textColor = TextPrimary, background = MakeTex(new Color(0.10f, 0.10f, 0.15f)) },
            focused = { textColor = TextPrimary, background = MakeTex(new Color(0.13f, 0.13f, 0.20f)) },
            padding = new RectOffset(6, 6, 4, 4)
        };
        #endregion

        #region Helper Methods
        private static GUIStyle CreateStyle(Color normal, RectOffset padding)
        {
            var style = new GUIStyle();
            style.normal.background = MakeTex(normal);
            style.padding = padding;
            return style;
        }

        public static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        public static void DrawRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, SolidWhite);
            GUI.color = prev;
        }

        public static void DrawBorder(Rect rect, Color color, float thickness = 1f)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public static void DrawHorizontalLine(Rect rect, Color color)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        }

        public static void ResetStyles()
        {
            _windowBackground = null; _header = null; _subHeader = null;
            _label = null; _labelSecondary = null; _labelBold = null;
            _card = null; _cardAlt = null; _buildButton = null;
            _buildButtonRunning = null; _cancelButton = null;
            _tab = null; _tabActive = null; _platformBadge = null;
            _logInfo = null; _logWarning = null; _logError = null; _logSuccess = null;
            _sectionTitle = null; _badge = null; _metricValue = null; _metricLabel = null;
            _smallButton = null; _textField = null;
        }
        #endregion
    }
}
#endif
