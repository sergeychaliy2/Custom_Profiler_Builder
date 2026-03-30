#if UNITY_EDITOR
using NexusBuildPro.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Strategies
{
    public sealed class MacOSBuildStrategy : BasePlatformBuildStrategy
    {
        public override string PlatformName => "macOS";
        public override string PlatformId => "macos";
        public override BuildTarget Target => BuildTarget.StandaloneOSX;
        public override BuildTargetGroup TargetGroup => BuildTargetGroup.Standalone;
        public override string DefaultOutputExtension => ".app";
        public override Color PlatformColor => new Color(0.65f, 0.65f, 0.65f, 1f);

        public override void PreparePlatform(BuildContext context)
        {
            base.PreparePlatform(context);
            context.LogInfo("macOS: Universal binary target.");
        }
    }
}
#endif
