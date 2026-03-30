#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Core
{
    /// <summary>ScriptableObject containing all configuration for a build target.</summary>
    [CreateAssetMenu(fileName = "NewBuildProfile", menuName = "NexusBuildPro/Build Profile", order = 1)]
    public sealed class BuildProfile : ScriptableObject
    {
        #region Serialized Fields
        [Header("Identity")]
        [SerializeField, Tooltip("Display name for this build profile")]
        private string _profileName = "New Profile";

        [SerializeField, Tooltip("Target platform for this profile")]
        private BuildTarget _buildTarget = BuildTarget.StandaloneWindows64;

        [Header("Output")]
        [SerializeField, Tooltip("Output directory path (relative to project or absolute)")]
        private string _outputPath = "Builds";

        [SerializeField, Tooltip("Executable name without extension")]
        private string _executableName = "Game";

        [Header("Build Options")]
        [SerializeField]
        private bool _developmentBuild = false;

        [SerializeField]
        private bool _allowDebugging = false;

        [SerializeField]
        private bool _connectProfiler = false;

        [SerializeField]
        private bool _buildAndRun = false;

        [SerializeField]
        private bool _cleanBuildDirectory = false;

        [SerializeField]
        private bool _incrementalBuild = true;

        [Header("Scenes")]
        [SerializeField, Tooltip("Scenes to include. Leave empty to use Build Settings scenes.")]
        private string[] _scenePaths = Array.Empty<string>();

        [Header("Optimization")]
        [SerializeField]
        private bool _enableTextureOptimization = true;

        [SerializeField]
        private bool _enableAudioOptimization = true;

        [SerializeField]
        private bool _enableMeshOptimization = false;

        [SerializeField]
        private bool _enableCodeStripping = true;

        [SerializeField]
        private ManagedStrippingLevel _strippingLevel = ManagedStrippingLevel.Minimal;

        [Header("Android Specific")]
        [SerializeField]
        private bool _buildApk = true;

        [SerializeField]
        private bool _buildAab = false;

        [SerializeField]
        private string _keystorePath = "";

        [SerializeField]
        private string _keystorePass = "";

        [SerializeField]
        private string _keyaliasName = "";

        [SerializeField]
        private string _keyaliasPass = "";

        [Header("WebGL Specific")]
        [SerializeField]
        private WebGLCompressionFormat _webGLCompression = WebGLCompressionFormat.Brotli;

        [Header("Metadata")]
        [SerializeField]
        private string _notes = "";

        [SerializeField]
        private string _version = "1.0.0";
        #endregion

        #region Properties
        public string ProfileName => _profileName;
        public BuildTarget BuildTarget => _buildTarget;
        public string OutputPath => _outputPath;
        public string ExecutableName => _executableName;
        public bool DevelopmentBuild => _developmentBuild;
        public bool AllowDebugging => _allowDebugging;
        public bool ConnectProfiler => _connectProfiler;
        public bool BuildAndRun => _buildAndRun;
        public bool CleanBuildDirectory => _cleanBuildDirectory;
        public bool IncrementalBuild => _incrementalBuild;
        public string[] ScenePaths => _scenePaths;
        public bool EnableTextureOptimization => _enableTextureOptimization;
        public bool EnableAudioOptimization => _enableAudioOptimization;
        public bool EnableMeshOptimization => _enableMeshOptimization;
        public bool EnableCodeStripping => _enableCodeStripping;
        public ManagedStrippingLevel StrippingLevel => _strippingLevel;
        public bool BuildApk => _buildApk;
        public bool BuildAab => _buildAab;
        public string KeystorePath => _keystorePath;
        public string KeystorePass => _keystorePass;
        public string KeyaliasName => _keyaliasName;
        public string KeyaliasPass => _keyaliasPass;
        public WebGLCompressionFormat WebGLCompression => _webGLCompression;
        public string Notes => _notes;
        public string Version => _version;
        #endregion

        #region Public Methods
        public string ResolveOutputPath()
        {
            if (System.IO.Path.IsPathRooted(_outputPath))
                return _outputPath;
            return System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath),
                _outputPath,
                _buildTarget.ToString());
        }

        public BuildOptions ComputeBuildOptions()
        {
            var opts = BuildOptions.None;
            if (_developmentBuild) opts |= BuildOptions.Development;
            if (_allowDebugging) opts |= BuildOptions.AllowDebugging;
            if (_connectProfiler) opts |= BuildOptions.ConnectWithProfiler;
            if (_buildAndRun) opts |= BuildOptions.AutoRunPlayer;
            if (_cleanBuildDirectory) opts |= BuildOptions.CleanBuildCache;
            return opts;
        }

        public string[] ResolveScenes()
        {
            if (_scenePaths != null && _scenePaths.Length > 0) return _scenePaths;

            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);
            return scenes.ToArray();
        }
        #endregion
    }
}
#endif
