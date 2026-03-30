#if UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Core
{
    /// <summary>Strategy interface for platform-specific build configuration.</summary>
    public interface IPlatformBuildStrategy
    {
        string PlatformName { get; }
        string PlatformId { get; }
        BuildTarget Target { get; }
        BuildTargetGroup TargetGroup { get; }
        bool IsModuleInstalled { get; }
        string DefaultOutputExtension { get; }
        Color PlatformColor { get; }

        BuildPlayerOptions ConfigureBuildOptions(BuildContext context);
        void PreparePlatform(BuildContext context);
        void CleanupPlatform(BuildContext context);
        bool ValidateConfiguration(BuildContext context, out string errorMessage);
    }
}
#endif
