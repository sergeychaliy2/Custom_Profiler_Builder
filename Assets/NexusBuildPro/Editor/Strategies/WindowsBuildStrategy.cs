#if UNITY_EDITOR
using NexusBuildPro.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Strategies
{
    public sealed class WindowsBuildStrategy : BasePlatformBuildStrategy
    {
        public override string PlatformName => "Windows x64";
        public override string PlatformId => "windows_x64";
        public override BuildTarget Target => BuildTarget.StandaloneWindows64;
        public override BuildTargetGroup TargetGroup => BuildTargetGroup.Standalone;
        public override string DefaultOutputExtension => ".exe";
        public override Color PlatformColor => new Color(0.0f, 0.47f, 0.84f, 1f); // Windows blue

        public override void PreparePlatform(BuildContext context)
        {
            base.PreparePlatform(context);
            context.LogInfo("Windows: Architecture x64 confirmed.");
        }
    }
}
#endif
