#if UNITY_EDITOR
using System;
using System.IO;
using NexusBuildPro.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Strategies
{
    /// <summary>Abstract base for all platform build strategies. Implements common logic.</summary>
    public abstract class BasePlatformBuildStrategy : IPlatformBuildStrategy
    {
        #region IPlatformBuildStrategy
        public abstract string PlatformName { get; }
        public abstract string PlatformId { get; }
        public abstract BuildTarget Target { get; }
        public abstract BuildTargetGroup TargetGroup { get; }
        public abstract string DefaultOutputExtension { get; }
        public abstract Color PlatformColor { get; }

        public virtual bool IsModuleInstalled =>
            BuildPipeline.IsBuildTargetSupported(TargetGroup, Target);

        public virtual BuildPlayerOptions ConfigureBuildOptions(BuildContext context)
        {
            var profile = context.Profile;
            var outputPath = BuildOutputPath(profile);

            return new BuildPlayerOptions
            {
                scenes = profile.ResolveScenes(),
                locationPathName = outputPath,
                target = Target,
                targetGroup = TargetGroup,
                options = profile.ComputeBuildOptions()
            };
        }

        public virtual void PreparePlatform(BuildContext context)
        {
            context.LogInfo($"Preparing platform: {PlatformName}");
        }

        public virtual void CleanupPlatform(BuildContext context)
        {
            context.LogInfo($"Cleanup complete for: {PlatformName}");
        }

        public virtual bool ValidateConfiguration(BuildContext context, out string errorMessage)
        {
            var scenes = context.Profile.ResolveScenes();
            if (scenes == null || scenes.Length == 0)
            {
                errorMessage = "No scenes configured. Add scenes to Build Settings or the profile.";
                return false;
            }
            errorMessage = null;
            return true;
        }
        #endregion

        #region Protected Methods
        protected string BuildOutputPath(BuildProfile profile)
        {
            var outputDir = profile.ResolveOutputPath();
            var name = string.IsNullOrEmpty(profile.ExecutableName) ? "Build" : profile.ExecutableName;

            if (!string.IsNullOrEmpty(DefaultOutputExtension))
                return Path.Combine(outputDir, $"{name}{DefaultOutputExtension}");
            return Path.Combine(outputDir, name);
        }
        #endregion
    }
}
#endif
