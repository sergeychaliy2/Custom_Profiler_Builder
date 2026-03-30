#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NexusBuildPro.Editor.Core;
using UnityEditor;

namespace NexusBuildPro.Editor.Steps
{
    /// <summary>Validates that all configured scenes exist and are accessible before the build.</summary>
    public sealed class SceneValidationStep : IBuildStep
    {
        public string StepName => "Scene Validation";
        public int Order => 10;
        public bool IsEnabled { get; set; } = true;

        public async Task<StepResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
        {
            var start = DateTime.UtcNow;
            await Task.CompletedTask;

            var scenes = context.Profile.ResolveScenes();
            if (scenes == null || scenes.Length == 0)
                return StepResult.Fail("No scenes found.", Elapsed(start));

            int errors = 0;
            foreach (var scenePath in scenes)
            {
                if (cancellationToken.IsCancellationRequested)
                    return StepResult.Fail("Cancelled.", Elapsed(start));

                if (!File.Exists(scenePath))
                {
                    context.LogError($"Scene not found: {scenePath}");
                    errors++;
                }
                else
                {
                    context.LogInfo($"  ✓ Scene OK: {Path.GetFileName(scenePath)}");
                }
            }

            if (errors > 0)
                return StepResult.Fail($"{errors} scene(s) missing.", Elapsed(start));

            return StepResult.Ok($"All {scenes.Length} scene(s) validated.", Elapsed(start));
        }

        private static float Elapsed(DateTime start) => (float)(DateTime.UtcNow - start).TotalSeconds;
    }
}
#endif
