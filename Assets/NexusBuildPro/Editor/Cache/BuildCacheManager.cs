#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Cache
{
    /// <summary>
    /// Manages incremental build caching via asset hash manifests.
    /// Prevents rebuilding assets that haven't changed.
    /// </summary>
    public sealed class BuildCacheManager
    {
        #region Constants
        private const string CacheDirectory = "Library/NexusBuildPro/Cache";
        private const string ManifestFileName = "build_manifest.json";
        private static readonly string ManifestPath = Path.Combine(CacheDirectory, ManifestFileName);
        #endregion

        #region Fields
        private BuildManifest _currentManifest;
        private BuildManifest _previousManifest;
        #endregion

        #region Properties
        public bool HasPreviousManifest => _previousManifest != null;
        public int CachedAssetCount => _previousManifest?.Entries.Count ?? 0;
        public string CacheDirectoryPath => CacheDirectory;
        #endregion

        #region Public Methods
        /// <summary>Loads the previous manifest and starts a new one.</summary>
        public void BeginSession()
        {
            EnsureCacheDirectoryExists();
            _previousManifest = LoadManifest();
            _currentManifest = new BuildManifest { CreatedAt = DateTime.UtcNow.ToString("O") };
        }

        /// <summary>Returns true if the asset at path has NOT changed since last build.</summary>
        public bool IsAssetCached(string assetPath)
        {
            if (_previousManifest == null) return false;
            var currentHash = ComputeFileHash(assetPath);
            if (currentHash == null) return false;

            if (!_previousManifest.Entries.TryGetValue(assetPath, out var entry)) return false;
            return entry.Hash == currentHash;
        }

        /// <summary>Records an asset hash into the current session manifest.</summary>
        public void RecordAsset(string assetPath)
        {
            if (_currentManifest == null) return;
            var hash = ComputeFileHash(assetPath);
            if (hash == null) return;
            _currentManifest.Entries[assetPath] = new ManifestEntry { Hash = hash, Path = assetPath };
        }

        /// <summary>Scans all project assets and records their hashes.</summary>
        public void ScanAllAssets(Action<float> onProgress = null)
        {
            var guids = AssetDatabase.FindAssets("t:Object");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                    RecordAsset(path);
                onProgress?.Invoke((float)i / guids.Length);
            }
        }

        /// <summary>Returns list of asset paths that have changed since last build.</summary>
        public List<string> GetChangedAssets()
        {
            var changed = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Object");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !IsAssetCached(path))
                    changed.Add(path);
            }
            return changed;
        }

        /// <summary>Saves the current session manifest to disk.</summary>
        public void CommitSession()
        {
            if (_currentManifest == null) return;
            try
            {
                EnsureCacheDirectoryExists();
                var json = JsonUtility.ToJson(_currentManifest, prettyPrint: true);
                File.WriteAllText(ManifestPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NexusBuildPro] Cache commit failed: {ex.Message}");
            }
        }

        public void ClearCache()
        {
            _previousManifest = null;
            _currentManifest = null;
            if (Directory.Exists(CacheDirectory))
                Directory.Delete(CacheDirectory, recursive: true);
        }

        public long GetCacheSizeBytes()
        {
            if (!Directory.Exists(CacheDirectory)) return 0;
            long total = 0;
            foreach (var f in Directory.GetFiles(CacheDirectory, "*", SearchOption.AllDirectories))
                total += new FileInfo(f).Length;
            return total;
        }
        #endregion

        #region Private Methods
        private BuildManifest LoadManifest()
        {
            if (!File.Exists(ManifestPath)) return null;
            try
            {
                var json = File.ReadAllText(ManifestPath);
                return JsonUtility.FromJson<BuildManifest>(json);
            }
            catch { return null; }
        }

        private string ComputeFileHash(string assetPath)
        {
            // assetPath is "Assets/..." relative to project root.
            // Application.dataPath ends at ".../Assets" — project root is its parent.
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot)) return null;

            var fullPath = Path.Combine(projectRoot, assetPath);
            if (!File.Exists(fullPath)) return null;
            try
            {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(fullPath);
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch { return null; }
        }

        private void EnsureCacheDirectoryExists()
        {
            if (!Directory.Exists(CacheDirectory))
                Directory.CreateDirectory(CacheDirectory);
        }
        #endregion
    }

    /// <summary>
    /// JsonUtility cannot serialize Dictionary directly, so we round-trip through a List.
    /// Entries dictionary is the live index; _entryList is the serialized payload.
    /// </summary>
    [Serializable]
    internal sealed class BuildManifest : UnityEngine.ISerializationCallbackReceiver
    {
        public string CreatedAt;

        [SerializeField] private List<ManifestEntry> _entryList = new();

        [NonSerialized]
        public Dictionary<string, ManifestEntry> Entries = new();

        public void OnBeforeSerialize()
        {
            _entryList.Clear();
            foreach (var kv in Entries) _entryList.Add(kv.Value);
        }

        public void OnAfterDeserialize()
        {
            Entries = new Dictionary<string, ManifestEntry>(_entryList.Count);
            foreach (var e in _entryList)
                if (!string.IsNullOrEmpty(e.Path)) Entries[e.Path] = e;
        }
    }

    [Serializable]
    internal sealed class ManifestEntry
    {
        public string Path;
        public string Hash;
    }
}
#endif
