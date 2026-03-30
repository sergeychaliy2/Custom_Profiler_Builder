#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NexusBuildPro.Editor.Core;
using UnityEditor;

namespace NexusBuildPro.Editor.Steps
{
    /// <summary>Post-build step: opens output folder, writes build report summary.</summary>
    public sealed class PostBuildStep : IBuildStep
    {
        public string StepName => "Post-Build Cleanup";
        public int Order => 200;
        public bool IsEnabled { get; set; } = true;

        public async Task<StepResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
        {
            var start = DateTime.UtcNow;
            await Task.CompletedTask;

            var outputDir = context.Profile.ResolveOutputPath();

            // Write a short build summary text file
            try
            {
                if (!string.IsNullOrEmpty(outputDir) && Directory.Exists(outputDir))
                {
                    var summaryPath = Path.Combine(outputDir, "build_summary.txt");
                    var lines = new[]
                    {
                        $"NexusBuildPro Build Summary",
                        $"===========================",
                        $"Profile: {context.Profile.ProfileName}",
                        $"Platform: {context.Strategy.PlatformName}",
                        $"Session: {context.SessionId}",
                        $"Built at: {DateTime.UtcNow:u}",
                        $"Version: {context.Profile.Version}",
                        $"Development: {context.Profile.DevelopmentBuild}",
                        $"Output: {outputDir}"
                    };
                    File.WriteAllLines(summaryPath, lines);
                    context.LogInfo($"Build summary written: {summaryPath}");
                }
            }
            catch (Exception ex)
            {
                context.LogWarning($"Could not write build summary: {ex.Message}");
            }

            return StepResult.Ok("Post-build complete.", Elapsed(start));
        }

        private static float Elapsed(DateTime start) => (float)(DateTime.UtcNow - start).TotalSeconds;
    }
}
#endif
