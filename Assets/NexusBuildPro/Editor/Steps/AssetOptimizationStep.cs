#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NexusBuildPro.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Steps
{
    /// <summary>
    /// Pre-build optimization step that applies texture compression overrides,
    /// audio quality reductions, and logs asset count metrics.
    /// </summary>
    public sealed class AssetOptimizationStep : IBuildStep
    {
        public string StepName => "Asset Optimization";
        public int Order => 20;
        public bool IsEnabled { get; set; } = true;

        public async Task<StepResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
        {
            var start = DateTime.UtcNow;
            await Task.CompletedTask;

            int processedCount = 0;
            long estimatedSavedBytes = 0;

            if (context.Profile.EnableTextureOptimization)
            {
                var (count, saved) = OptimizeTextures(context, cancellationToken);
                processedCount += count;
                estimatedSavedBytes += saved;
            }

            if (cancellationToken.IsCancellationRequested)
                return StepResult.Fail("Cancelled during optimization.", Elapsed(start));

            if (context.Profile.EnableAudioOptimization)
            {
                var (count, saved) = OptimizeAudio(context, cancellationToken);
                processedCount += count;
                estimatedSavedBytes += saved;
            }

            if (context.Profile.EnableMeshOptimization)
            {
                var count = OptimizeMeshes(context, cancellationToken);
                processedCount += count;
            }

            context.SetMetadata("OptimizedAssetCount", processedCount);
            context.SetMetadata("EstimatedSavedBytes", estimatedSavedBytes);

            string savedStr = FormatBytes(estimatedSavedBytes);
            context.LogSuccess($"Optimization complete: {processedCount} assets processed, ~{savedStr} saved.");
            return StepResult.Ok($"Processed {processedCount} assets", Elapsed(start));
        }

        private (int count, long savedBytes) OptimizeTextures(BuildContext context, CancellationToken ct)
        {
            int count = 0;
            long saved = 0;
            var guids = AssetDatabase.FindAssets("t:Texture2D");

            foreach (var guid in guids)
            {
                if (ct.IsCancellationRequested) break;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;

                // Enable mipmap streaming if not already
                if (!importer.mipmapEnabled && importer.textureType == TextureImporterType.Default)
                {
                    importer.mipmapEnabled = true;
                    changed = true;
                }

                // Ensure max size is sane (don't go above 2048 for mobile-friendly)
                var platformSettings = importer.GetDefaultPlatformTextureSettings();
                if (platformSettings.maxTextureSize > 2048)
                {
                    platformSettings.maxTextureSize = 2048;
                    importer.SetPlatformTextureSettings(platformSettings);
                    changed = true;
                    saved += 1024 * 1024; // rough estimate
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    count++;
                }
            }
            context.LogInfo($"  Textures: {count} optimized.");
            return (count, saved);
        }

        private (int count, long savedBytes) OptimizeAudio(BuildContext context, CancellationToken ct)
        {
            int count = 0;
            long saved = 0;
            var guids = AssetDatabase.FindAssets("t:AudioClip");

            foreach (var guid in guids)
            {
                if (ct.IsCancellationRequested) break;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                var settings = importer.defaultSampleSettings;
                bool changed = false;

                if (settings.quality > 0.7f)
                {
                    settings.quality = 0.65f;
                    importer.defaultSampleSettings = settings;
                    changed = true;
                    saved += 512 * 1024;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    count++;
                }
            }
            context.LogInfo($"  Audio: {count} optimized.");
            return (count, saved);
        }

        private int OptimizeMeshes(BuildContext context, CancellationToken ct)
        {
            int count = 0;
            var guids = AssetDatabase.FindAssets("t:Mesh");
            foreach (var guid in guids)
            {
                if (ct.IsCancellationRequested) break;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                bool changed = false;
                if (!importer.meshCompression.Equals(ModelImporterMeshCompression.Low))
                {
                    importer.meshCompression = ModelImporterMeshCompression.Low;
                    changed = true;
                }

                if (changed) { importer.SaveAndReimport(); count++; }
            }
            context.LogInfo($"  Meshes: {count} optimized.");
            return count;
        }

        private static float Elapsed(DateTime start) => (float)(DateTime.UtcNow - start).TotalSeconds;

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
            return $"{bytes / (1024 * 1024)} MB";
        }
    }
}
#endif
