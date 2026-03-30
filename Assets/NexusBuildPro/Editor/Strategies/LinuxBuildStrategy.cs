#if UNITY_EDITOR
using NexusBuildPro.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Strategies
{
    public sealed class LinuxBuildStrategy : BasePlatformBuildStrategy
    {
        public override string PlatformName => "Linux x64";
        public override string PlatformId => "linux_x64";
        public override BuildTarget Target => BuildTarget.StandaloneLinux64;
        public override BuildTargetGroup TargetGroup => BuildTargetGroup.Standalone;
        public override string DefaultOutputExtension => "";
        public override Color PlatformColor => new Color(0.95f, 0.6f, 0.0f, 1f); // Linux orange

        public override void PreparePlatform(BuildContext context)
        {
            base.PreparePlatform(context);
            context.LogInfo("Linux: x86_64 target.");
        }
    }
}
#endif
