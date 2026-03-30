#if UNITY_EDITOR
using NexusBuildPro.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Strategies
{
    public sealed class WebGLBuildStrategy : BasePlatformBuildStrategy
    {
        public override string PlatformName => "WebGL";
        public override string PlatformId => "webgl";
        public override BuildTarget Target => BuildTarget.WebGL;
        public override BuildTargetGroup TargetGroup => BuildTargetGroup.WebGL;
        public override string DefaultOutputExtension => "";
        public override Color PlatformColor => new Color(0.98f, 0.45f, 0.09f, 1f); // WebGL orange-red

        public override void PreparePlatform(BuildContext context)
        {
            base.PreparePlatform(context);
            PlayerSettings.WebGL.compressionFormat = context.Profile.WebGLCompression;
            context.LogInfo($"WebGL: Compression = {context.Profile.WebGLCompression}");
            context.LogInfo("WebGL: Ensure a local HTTP server to test the build.");
        }

        public override bool ValidateConfiguration(BuildContext context, out string errorMessage)
        {
            if (!base.ValidateConfiguration(context, out errorMessage)) return false;

            // WebGL doesn't support threads with certain compression settings
            if (PlayerSettings.WebGL.threadsSupport &&
                context.Profile.WebGLCompression == WebGLCompressionFormat.Brotli)
            {
                context.LogWarning("WebGL: Brotli + threads may require server-side COOP/COEP headers.");
            }
            return true;
        }
    }
}
#endif
