#if UNITY_EDITOR
using System.IO;
using NexusBuildPro.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace NexusBuildPro.Editor.Strategies
{
    public sealed class AndroidBuildStrategy : BasePlatformBuildStrategy
    {
        public override string PlatformName => "Android";
        public override string PlatformId => "android";
        public override BuildTarget Target => BuildTarget.Android;
        public override BuildTargetGroup TargetGroup => BuildTargetGroup.Android;
        public override string DefaultOutputExtension => ".apk";
        public override Color PlatformColor => new Color(0.21f, 0.65f, 0.32f, 1f); // Android green

        public override void PreparePlatform(BuildContext context)
        {
            base.PreparePlatform(context);
            var profile = context.Profile;

            // Configure keystore
            if (!string.IsNullOrEmpty(profile.KeystorePath) && File.Exists(profile.KeystorePath))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = profile.KeystorePath;
                PlayerSettings.Android.keystorePass = profile.KeystorePass;
                PlayerSettings.Android.keyaliasName = profile.KeyaliasName;
                PlayerSettings.Android.keyaliasPass = profile.KeyaliasPass;
                context.LogInfo("Android: Custom keystore configured.");
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                context.LogWarning("Android: Using debug keystore (no custom keystore provided).");
            }

            // AAB vs APK
            EditorUserBuildSettings.buildAppBundle = profile.BuildAab;
            context.LogInfo($"Android: Build type = {(profile.BuildAab ? "AAB" : "APK")}");
        }

        public override BuildPlayerOptions ConfigureBuildOptions(BuildContext context)
        {
            var opts = base.ConfigureBuildOptions(context);
            if (context.Profile.BuildAab)
                opts.locationPathName = opts.locationPathName.Replace(".apk", ".aab");
            return opts;
        }

        public override bool ValidateConfiguration(BuildContext context, out string errorMessage)
        {
            if (!base.ValidateConfiguration(context, out errorMessage)) return false;

            if (string.IsNullOrEmpty(PlayerSettings.applicationIdentifier))
            {
                errorMessage = "Android: Package name (applicationIdentifier) is not set.";
                return false;
            }
            return true;
        }
    }
}
#endif
