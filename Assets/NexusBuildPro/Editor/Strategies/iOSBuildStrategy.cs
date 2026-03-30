#if UNITY_EDITOR
using NexusBuildPro.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Strategies
{
    public sealed class iOSBuildStrategy : BasePlatformBuildStrategy
    {
        public override string PlatformName => "iOS";
        public override string PlatformId => "ios";
        public override BuildTarget Target => BuildTarget.iOS;
        public override BuildTargetGroup TargetGroup => BuildTargetGroup.iOS;
        public override string DefaultOutputExtension => "";
        public override Color PlatformColor => new Color(0.55f, 0.55f, 0.55f, 1f);

        public override void PreparePlatform(BuildContext context)
        {
            base.PreparePlatform(context);
            context.LogInfo("iOS: Generating Xcode project.");
            context.LogWarning("iOS: Final .ipa requires Xcode on macOS.");
        }

        public override bool ValidateConfiguration(BuildContext context, out string errorMessage)
        {
            if (!base.ValidateConfiguration(context, out errorMessage)) return false;

            if (string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID))
            {
                errorMessage = null; // Warning only, not fatal
                context.LogWarning("iOS: No Apple Developer Team ID set. You'll need to set this in Xcode.");
            }
            return true;
        }
    }
}
#endif
