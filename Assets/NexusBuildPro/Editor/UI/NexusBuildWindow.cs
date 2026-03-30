#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NexusBuildPro.Editor.Core;
using NexusBuildPro.Editor.Steps;
using NexusBuildPro.Editor.Strategies;
using NexusBuildPro.Editor.UI.Views;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.UI
{
    /// <summary>
    /// NexusBuildPro — Enterprise cross-platform build orchestration framework.
    /// Main editor window with dashboard, platform config, profiler, history, and optimizer views.
    /// </summary>
    public sealed class NexusBuildWindow : EditorWindow
    {
        #region Constants
        private const string WindowTitle = "NexusBuildPro";
        private const string Version = "1.0.0";
        private const float SidebarWidth = 220f;
        private const float HeaderHeight = 56f;
        private const float FooterHeight = 180f;
        private const float TabBarHeight = 34f;
        private const float StatusBarHeight = 22f;
        private const float BuildButtonHeight = 44f;
        #endregion

        #region Serialized State
        [SerializeField] private int _activeTab = 0;
        [SerializeField] private int _selectedProfileIndex = 0;
        [SerializeField] private List<BuildProfile> _profiles = new();
        #endregion

        #region Runtime Fields
        private BuildOrchestrator _orchestrator;
        private List<IPlatformBuildStrategy> _strategies;
        private IPlatformBuildStrategy _activeStrategy;
        private BuildResult _lastResult;

        private DashboardView _dashboardView;
        private PlatformConfigView _platformConfigView;
        private BuildHistoryView _historyView;
        private OptimizationView _optimizationView;
        private ProfilerView _profilerView;

        private Vector2 _sidebarScroll;
        private Vector2 _logScroll;
        private readonly List<BuildLogEntry> _liveLog = new();
        private bool _autoScrollLog = true;
        private float _buildProgress;
        private string _buildStepName = "Ready";
        private float _animTime;
        private bool _isInitialized;

        // Build trigger flag — set in OnGUI, consumed in OnEditorUpdate (outside player loop)
        private bool _pendingBuildTrigger;
        private BuildProfile _pendingProfile;
        private IPlatformBuildStrategy _pendingStrategy;
        #endregion

        #region Tab Definitions
        private static readonly string[] TabNames = { "Dashboard", "Platforms", "Optimizer", "Profiler", "History" };
        private static readonly string[] TabIcons = { "●", "◈", "⚡", "📊", "📋" };
        #endregion

        #region Menu
        [MenuItem("Tools/NexusBuildPro/Open NexusBuildPro %#b", priority = 1)]
        public static void OpenWindow()
        {
            var window = GetWindow<NexusBuildWindow>(WindowTitle);
            window.minSize = new Vector2(900f, 600f);
            window.Show();
        }

        [MenuItem("Tools/NexusBuildPro/Quick Build (Active Platform) %#&b", priority = 2)]
        public static void QuickBuild()
        {
            var window = GetWindow<NexusBuildWindow>(WindowTitle);
            window.Show();
            window.TriggerBuild();
        }
        #endregion

        #region Lifecycle
        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            NexusStyles.ResetStyles();
            InitializeSystems();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _orchestrator?.CancelBuild();
            UnsubscribeOrchestratorEvents();
        }

        private void OnDestroy()
        {
            _orchestrator?.CancelBuild();
            UnsubscribeOrchestratorEvents();
        }
        #endregion

        #region OnGUI
        private void OnGUI()
        {
            if (!_isInitialized) { InitializeSystems(); }

            _animTime = (float)EditorApplication.timeSinceStartup;

            // Background
            NexusStyles.DrawRect(new Rect(0, 0, position.width, position.height), NexusStyles.Background);

            DrawHeader(new Rect(0, 0, position.width, HeaderHeight));
            DrawTabBar(new Rect(0, HeaderHeight, position.width, TabBarHeight));

            float bodyY = HeaderHeight + TabBarHeight;
            float bodyH = position.height - bodyY - FooterHeight - StatusBarHeight;
            float sidebarX = 0;
            float contentX = SidebarWidth + 1f;
            float contentW = position.width - SidebarWidth - 1f;

            DrawSidebar(new Rect(sidebarX, bodyY, SidebarWidth, bodyH));
            NexusStyles.DrawRect(new Rect(SidebarWidth, bodyY, 1f, bodyH), NexusStyles.Border);
            DrawMainContent(new Rect(contentX, bodyY, contentW, bodyH));

            DrawFooter(new Rect(0, position.height - FooterHeight - StatusBarHeight, position.width, FooterHeight));
            DrawStatusBar(new Rect(0, position.height - StatusBarHeight, position.width, StatusBarHeight));
        }
        #endregion

        #region Header
        private void DrawHeader(Rect rect)
        {
            NexusStyles.DrawRect(rect, NexusStyles.Surface);
            NexusStyles.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), NexusStyles.Border);

            // Left: Logo + Title
            NexusStyles.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), NexusStyles.Accent);

            GUI.Label(new Rect(rect.x + 16f, rect.y + 8f, 200f, 26f), "NEXUS", NexusStyles.Header);
            GUI.Label(new Rect(rect.x + 82f, rect.y + 8f, 80f, 26f),
                new GUIContent("BUILD PRO"),
                new GUIStyle(NexusStyles.Header) { normal = { textColor = NexusStyles.Accent } });

            GUI.Label(new Rect(rect.x + 16f, rect.y + 36f, 200f, 14f), $"v{Version}",
                NexusStyles.LabelSecondary);

            // Right: Build status indicator
            float rightX = rect.xMax - 260f;
            if (_orchestrator.IsBuilding)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(_animTime * 4f);
                NexusStyles.DrawRect(new Rect(rightX, rect.y + 18f, 12f, 12f),
                    new Color(NexusStyles.Warning.r, NexusStyles.Warning.g, NexusStyles.Warning.b, pulse));
                GUI.Label(new Rect(rightX + 16f, rect.y + 12f, 130f, 20f),
                    "BUILD IN PROGRESS", new GUIStyle(NexusStyles.LabelBold) { normal = { textColor = NexusStyles.Warning } });
                GUI.Label(new Rect(rightX + 16f, rect.y + 30f, 180f, 14f),
                    _buildStepName, NexusStyles.LabelSecondary);
            }
            else if (_lastResult != null)
            {
                var resultColor = _lastResult.Success ? NexusStyles.Success : NexusStyles.Error;
                var resultText = _lastResult.Success ? "LAST BUILD: SUCCESS" : "LAST BUILD: FAILED";
                NexusStyles.DrawRect(new Rect(rightX, rect.y + 18f, 12f, 12f), resultColor);
                GUI.Label(new Rect(rightX + 16f, rect.y + 12f, 180f, 20f), resultText,
                    new GUIStyle(NexusStyles.LabelBold) { normal = { textColor = resultColor } });
                GUI.Label(new Rect(rightX + 16f, rect.y + 30f, 200f, 14f),
                    $"{_lastResult.FormatDuration()} · {_lastResult.FormatSize()}", NexusStyles.LabelSecondary);
            }

            // Active platform badge
            if (_activeStrategy != null)
            {
                float badgeX = rect.xMax - 120f;
                var badgeRect = new Rect(badgeX, rect.y + 14f, 110f, 26f);
                NexusStyles.DrawRect(badgeRect,
                    new Color(_activeStrategy.PlatformColor.r, _activeStrategy.PlatformColor.g,
                        _activeStrategy.PlatformColor.b, 0.15f));
                NexusStyles.DrawBorder(badgeRect, _activeStrategy.PlatformColor, 1f);
                GUI.Label(badgeRect, _activeStrategy.PlatformName,
                    new GUIStyle(NexusStyles.LabelBold)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = _activeStrategy.PlatformColor }
                    });
            }
        }
        #endregion

        #region Tab Bar
        private void DrawTabBar(Rect rect)
        {
            NexusStyles.DrawRect(rect, new Color(0.10f, 0.10f, 0.15f, 1f));
            NexusStyles.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), NexusStyles.Border);

            float tabW = 120f;
            float startX = rect.x + 8f;

            for (int i = 0; i < TabNames.Length; i++)
            {
                bool isActive = _activeTab == i;
                var tabRect = new Rect(startX + i * (tabW + 4f), rect.y + 4f, tabW, rect.height - 8f);

                if (isActive)
                {
                    NexusStyles.DrawRect(tabRect, NexusStyles.Accent);
                    NexusStyles.DrawRect(new Rect(tabRect.x, tabRect.yMax, tabRect.width, 1f), NexusStyles.AccentHot);
                }

                if (GUI.Button(tabRect, $"{TabIcons[i]}  {TabNames[i]}", isActive ? NexusStyles.TabActive : NexusStyles.Tab))
                    _activeTab = i;
            }

            // Version label at right
            GUI.Label(new Rect(rect.xMax - 140f, rect.y, 130f, rect.height),
                "Cross-Platform Build Forge", new GUIStyle(NexusStyles.LabelSecondary)
                { alignment = TextAnchor.MiddleRight, fontSize = 9 });
        }
        #endregion

        #region Sidebar
        private void DrawSidebar(Rect rect)
        {
            NexusStyles.DrawRect(rect, new Color(0.12f, 0.12f, 0.18f, 1f));

            float y = rect.y + 8f;

            // ---- BUILD PROFILE SECTION ----
            GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 16f), "BUILD PROFILES", NexusStyles.SectionTitle);
            y += 20f;

            // Scan for profiles
            if (GUI.Button(new Rect(rect.x + 8f, y, rect.width - 16f, 22f),
                "↺  Refresh Profiles", NexusStyles.SmallButton))
            {
                RefreshProfiles();
            }
            y += 28f;

            if (_profiles.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 30f),
                    "No profiles found.\nCreate via Assets menu.", NexusStyles.LabelSecondary);
                y += 36f;
            }

            _sidebarScroll = GUI.BeginScrollView(
                new Rect(rect.x, y, rect.width, 200f), _sidebarScroll,
                new Rect(0, 0, rect.width - 16f, _profiles.Count * 54f + 4f));

            for (int i = 0; i < _profiles.Count; i++)
            {
                var profile = _profiles[i];
                if (profile == null) continue;

                bool isSelected = _selectedProfileIndex == i;
                var profileRect = new Rect(0, i * 54f + 2f, rect.width - 16f, 50f);

                NexusStyles.DrawRect(profileRect, isSelected ? NexusStyles.SurfaceAlt : NexusStyles.Surface);
                NexusStyles.DrawBorder(profileRect, isSelected ? NexusStyles.Accent : NexusStyles.Border,
                    isSelected ? 1.5f : 1f);

                if (isSelected)
                    NexusStyles.DrawRect(new Rect(profileRect.x, profileRect.y, 3f, profileRect.height), NexusStyles.Accent);

                GUI.Label(new Rect(profileRect.x + 8f, profileRect.y + 6f, profileRect.width - 12f, 16f),
                    profile.ProfileName, NexusStyles.LabelBold);
                GUI.Label(new Rect(profileRect.x + 8f, profileRect.y + 24f, profileRect.width - 12f, 14f),
                    $"{profile.BuildTarget} · v{profile.Version}", NexusStyles.LabelSecondary);

                if (GUI.Button(profileRect, GUIContent.none, GUIStyle.none))
                {
                    _selectedProfileIndex = i;
                    SyncStrategyToProfile(profile);
                }
            }
            GUI.EndScrollView();

            y += 208f;

            // ---- CREATE PROFILE BUTTON ----
            if (GUI.Button(new Rect(rect.x + 8f, y, rect.width - 16f, 22f),
                "+ New Build Profile", NexusStyles.SmallButton))
            {
                CreateNewProfile();
            }
            y += 30f;

            NexusStyles.DrawRect(new Rect(rect.x + 8f, y, rect.width - 16f, 1f), NexusStyles.Separator);
            y += 12f;

            // ---- ACTIVE PLATFORM SECTION ----
            GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 16f), "ACTIVE PLATFORM", NexusStyles.SectionTitle);
            y += 20f;

            if (_activeStrategy != null)
            {
                var platformRect = new Rect(rect.x + 8f, y, rect.width - 16f, 60f);
                NexusStyles.DrawRect(platformRect,
                    new Color(_activeStrategy.PlatformColor.r, _activeStrategy.PlatformColor.g,
                        _activeStrategy.PlatformColor.b, 0.08f));
                NexusStyles.DrawBorder(platformRect, _activeStrategy.PlatformColor, 1.5f);
                NexusStyles.DrawRect(new Rect(platformRect.x, platformRect.y, 4f, platformRect.height),
                    _activeStrategy.PlatformColor);

                GUI.Label(new Rect(platformRect.x + 10f, platformRect.y + 6f, platformRect.width - 14f, 20f),
                    _activeStrategy.PlatformName,
                    new GUIStyle(NexusStyles.LabelBold) { normal = { textColor = _activeStrategy.PlatformColor } });

                var modColor = _activeStrategy.IsModuleInstalled ? NexusStyles.Success : NexusStyles.Error;
                GUI.Label(new Rect(platformRect.x + 10f, platformRect.y + 26f, platformRect.width - 14f, 14f),
                    _activeStrategy.IsModuleInstalled ? "✓ Module Installed" : "✗ Module Not Installed",
                    new GUIStyle(NexusStyles.LabelSecondary) { normal = { textColor = modColor }, fontSize = 9 });

                GUI.Label(new Rect(platformRect.x + 10f, platformRect.y + 42f, platformRect.width - 14f, 14f),
                    _activeStrategy.Target.ToString(),
                    new GUIStyle(NexusStyles.LabelSecondary) { fontSize = 9 });
                y += 68f;
            }
            else
            {
                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 30f),
                    "No platform selected.\nGo to Platforms tab.", NexusStyles.LabelSecondary);
                y += 36f;
            }

            y += 8f;

            // ---- BUILD BUTTON ----
            DrawBuildButton(new Rect(rect.x + 8f, y, rect.width - 16f, BuildButtonHeight));
            y += BuildButtonHeight + 6f;

            // ---- PROGRESS BAR ----
            if (_orchestrator.IsBuilding)
            {
                var progressBg = new Rect(rect.x + 8f, y, rect.width - 16f, 10f);
                NexusStyles.DrawRect(progressBg, NexusStyles.Border);
                float fillW = progressBg.width * _buildProgress;

                // Animated shimmer
                float shimmer = (Mathf.Sin(_animTime * 3f) + 1f) * 0.5f;
                NexusStyles.DrawRect(new Rect(progressBg.x, progressBg.y, fillW, 10f), NexusStyles.Accent);
                NexusStyles.DrawRect(new Rect(progressBg.x + fillW - 8f, progressBg.y, 8f, 10f),
                    new Color(1f, 1f, 1f, shimmer * 0.3f));

                GUI.Label(new Rect(rect.x + 8f, y + 12f, rect.width - 16f, 14f),
                    $"{_buildProgress * 100f:F0}%  {_buildStepName}",
                    new GUIStyle(NexusStyles.LabelSecondary) { fontSize = 9 });
                y += 30f;

                // Cancel button
                if (GUI.Button(new Rect(rect.x + 8f, y, rect.width - 16f, 22f),
                    "■ Cancel Build", NexusStyles.CancelButton))
                {
                    _orchestrator.CancelBuild();
                }
            }
        }

        private void DrawBuildButton(Rect rect)
        {
            if (_orchestrator.IsBuilding)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(_animTime * 5f);
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, pulse);
                GUI.Button(rect, "⟳  BUILDING...", NexusStyles.BuildButtonRunning);
                GUI.color = prevColor;
                return;
            }

            bool canBuild = _activeStrategy != null && _profiles.Count > 0 && _selectedProfileIndex < _profiles.Count;

            GUI.enabled = canBuild;
            if (GUI.Button(rect, canBuild ? "▶  BUILD NOW" : "Select Platform & Profile", NexusStyles.BuildButton))
            {
                TriggerBuild();
            }
            GUI.enabled = true;
        }
        #endregion

        #region Main Content
        private void DrawMainContent(Rect rect)
        {
            float pad = 8f;
            var contentRect = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);

            switch (_activeTab)
            {
                case 0: _dashboardView?.Draw(contentRect); break;
                case 1: _platformConfigView?.Draw(contentRect); break;
                case 2: _optimizationView?.Draw(contentRect); break;
                case 3: _profilerView?.Draw(contentRect); break;
                case 4: _historyView?.Draw(contentRect); break;
            }
        }
        #endregion

        #region Footer (Build Log)
        private void DrawFooter(Rect rect)
        {
            NexusStyles.DrawRect(rect, new Color(0.08f, 0.08f, 0.12f, 1f));
            NexusStyles.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), NexusStyles.Border);

            // Log toolbar
            var toolbarRect = new Rect(rect.x, rect.y, rect.width, 24f);
            NexusStyles.DrawRect(toolbarRect, NexusStyles.Surface);

            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, 80f, 16f), "BUILD LOG", NexusStyles.SectionTitle);

            int infoCount = 0, warnCount = 0, errCount = 0;
            foreach (var e in _liveLog)
            {
                if (e.Level == BuildLogLevel.Error) errCount++;
                else if (e.Level == BuildLogLevel.Warning) warnCount++;
                else infoCount++;
            }

            GUI.Label(new Rect(rect.x + 96f, rect.y + 5f, 60f, 14f),
                $"ℹ {infoCount}", new GUIStyle(NexusStyles.LabelSecondary) { fontSize = 10 });
            GUI.Label(new Rect(rect.x + 150f, rect.y + 5f, 60f, 14f),
                $"⚠ {warnCount}", new GUIStyle(NexusStyles.LabelSecondary)
                { fontSize = 10, normal = { textColor = warnCount > 0 ? NexusStyles.Warning : NexusStyles.TextDisabled } });
            GUI.Label(new Rect(rect.x + 204f, rect.y + 5f, 60f, 14f),
                $"✗ {errCount}", new GUIStyle(NexusStyles.LabelSecondary)
                { fontSize = 10, normal = { textColor = errCount > 0 ? NexusStyles.Error : NexusStyles.TextDisabled } });

            _autoScrollLog = GUI.Toggle(new Rect(rect.xMax - 130f, rect.y + 5f, 120f, 14f),
                _autoScrollLog, " Auto-scroll", NexusStyles.LabelSecondary);

            if (GUI.Button(new Rect(rect.xMax - 60f, rect.y + 4f, 55f, 16f), "Clear", NexusStyles.SmallButton))
                _liveLog.Clear();

            // Log entries
            float logY = rect.y + 26f;
            float logH = rect.height - 26f;
            float lineH = 15f;
            float totalH = Mathf.Max(_liveLog.Count * lineH, logH);

            if (_autoScrollLog && _liveLog.Count > 0)
                _logScroll.y = Mathf.Max(0, _liveLog.Count * lineH - logH);

            _logScroll = GUI.BeginScrollView(
                new Rect(rect.x, logY, rect.width, logH), _logScroll,
                new Rect(0, 0, rect.width - 16f, totalH));

            for (int i = 0; i < _liveLog.Count; i++)
            {
                var entry = _liveLog[i];
                var style = entry.Level switch
                {
                    BuildLogLevel.Error => NexusStyles.LogError,
                    BuildLogLevel.Warning => NexusStyles.LogWarning,
                    BuildLogLevel.Success => NexusStyles.LogSuccess,
                    _ => NexusStyles.LogInfo
                };

                string prefix = entry.Level switch
                {
                    BuildLogLevel.Error => "[ERR] ",
                    BuildLogLevel.Warning => "[WRN] ",
                    BuildLogLevel.Success => "[OK]  ",
                    _ => "[INF] "
                };

                string timestamp = entry.Timestamp.ToString("HH:mm:ss");
                GUI.Label(new Rect(4f, i * lineH, rect.width - 20f, lineH),
                    $"{timestamp}  {prefix}{entry.Message}", style);
            }

            GUI.EndScrollView();
        }
        #endregion

        #region Status Bar
        private void DrawStatusBar(Rect rect)
        {
            NexusStyles.DrawRect(rect, new Color(0.07f, 0.07f, 0.10f, 1f));
            NexusStyles.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), NexusStyles.Border);

            // Left: current action
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, 400f, 14f),
                _orchestrator.IsBuilding ? $"⚙  {_buildStepName}" : "Ready",
                new GUIStyle(NexusStyles.LabelSecondary) { fontSize = 10 });

            // Right: info
            string info = $"Profiles: {_profiles.Count}  |  Platforms: {_strategies.Count}  |  NexusBuildPro v{Version}";
            GUI.Label(new Rect(rect.xMax - 400f, rect.y + 4f, 392f, 14f), info,
                new GUIStyle(NexusStyles.LabelSecondary)
                { alignment = TextAnchor.MiddleRight, fontSize = 10 });
        }
        #endregion

        #region Build Triggering
        private void TriggerBuild()
        {
            if (_orchestrator.IsBuilding) return;
            if (_activeStrategy == null || _profiles.Count == 0) return;

            var profile = _profiles[_selectedProfileIndex];
            if (profile == null) return;

            _liveLog.Clear();
            _buildProgress = 0f;
            _buildStepName = "Queued...";
            Repaint();

            // Capture for closure
            var capturedProfile = profile;
            var capturedStrategy = _activeStrategy;

            // delayCall fires outside OnGUI. BuildAsync has NO await before BuildPipeline.BuildPlayer,
            // so BuildPlayer runs synchronously in this delayCall — outside Unity 6's player loop check.
            EditorApplication.delayCall += () =>
            {
                _ = _orchestrator.BuildAsync(capturedProfile, capturedStrategy);
            };
        }
        #endregion

        #region Initialization
        private void InitializeSystems()
        {
            _orchestrator = new BuildOrchestrator();
            SubscribeOrchestratorEvents();

            _strategies = BuildStrategyRegistry.GetAllStrategies();

            // Register default build steps
            _orchestrator.AddStep(new SceneValidationStep());
            _orchestrator.AddStep(new AssetOptimizationStep());
            _orchestrator.AddStep(new PostBuildStep());

            // Init views
            _dashboardView = new DashboardView(_orchestrator);
            _platformConfigView = new PlatformConfigView(_strategies);
            _historyView = new BuildHistoryView(_orchestrator);
            _optimizationView = new OptimizationView(_orchestrator);
            _profilerView = new ProfilerView(_orchestrator);

            _platformConfigView.OnPlatformSelected += strategy =>
            {
                _activeStrategy = strategy;
                Repaint();
            };

            RefreshProfiles();

            // Default platform: Windows
            if (_strategies.Count > 0)
                _activeStrategy = _strategies[0];

            _isInitialized = true;
        }

        private void RefreshProfiles()
        {
            _profiles.Clear();
            var guids = AssetDatabase.FindAssets("t:BuildProfile");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                if (profile != null) _profiles.Add(profile);
            }
            _selectedProfileIndex = Mathf.Clamp(_selectedProfileIndex, 0, Mathf.Max(0, _profiles.Count - 1));
        }

        private void SyncStrategyToProfile(BuildProfile profile)
        {
            foreach (var s in _strategies)
            {
                if (s.Target == profile.BuildTarget)
                {
                    _activeStrategy = s;
                    _platformConfigView.SelectStrategy(s);
                    break;
                }
            }
        }

        private void CreateNewProfile()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Build Profile", "NewBuildProfile", "asset",
                "Choose where to save the Build Profile");
            if (string.IsNullOrEmpty(path)) return;

            var profile = CreateInstance<BuildProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            RefreshProfiles();
            Selection.activeObject = profile;
        }
        #endregion

        #region Orchestrator Event Wiring
        private void SubscribeOrchestratorEvents()
        {
            _orchestrator.OnProgressChanged += OnBuildProgress;
            _orchestrator.OnLogEntry += OnBuildLogEntry;
            _orchestrator.OnBuildCompleted += OnBuildCompleted;
            _orchestrator.OnBuildStarted += OnBuildStarted;
            _orchestrator.OnBuildCancelled += OnBuildCancelled;
        }

        private void UnsubscribeOrchestratorEvents()
        {
            if (_orchestrator == null) return;
            _orchestrator.OnProgressChanged -= OnBuildProgress;
            _orchestrator.OnLogEntry -= OnBuildLogEntry;
            _orchestrator.OnBuildCompleted -= OnBuildCompleted;
            _orchestrator.OnBuildStarted -= OnBuildStarted;
            _orchestrator.OnBuildCancelled -= OnBuildCancelled;
        }

        private void OnBuildProgress(float progress, string stepName)
        {
            _buildProgress = progress;
            _buildStepName = stepName;
            Repaint();
        }

        private void OnBuildLogEntry(BuildLogEntry entry)
        {
            _liveLog.Add(entry);
            if (_liveLog.Count > 500) _liveLog.RemoveAt(0);
            Repaint();
        }

        private void OnBuildCompleted(BuildResult result)
        {
            _lastResult = result;
            _buildProgress = result.Success ? 1f : _buildProgress;
            _buildStepName = result.Success ? "Complete" : "Failed";
            Repaint();
        }

        private void OnBuildStarted() { _buildProgress = 0f; Repaint(); }
        private void OnBuildCancelled() { _buildStepName = "Cancelled"; Repaint(); }
        #endregion

        #region Editor Update
        private void OnEditorUpdate()
        {
            if (_orchestrator.IsBuilding) Repaint();
        }
        #endregion
    }

    /// <summary>Registry that instantiates all available platform strategies.</summary>
    public static class BuildStrategyRegistry
    {
        public static List<IPlatformBuildStrategy> GetAllStrategies() => new()
        {
            new WindowsBuildStrategy(),
            new MacOSBuildStrategy(),
            new LinuxBuildStrategy(),
            new AndroidBuildStrategy(),
            new iOSBuildStrategy(),
            new WebGLBuildStrategy(),
        };
    }
}
#endif
