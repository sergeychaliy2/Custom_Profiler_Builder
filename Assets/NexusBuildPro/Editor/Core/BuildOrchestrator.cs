#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexusBuildPro.Editor.Cache;
using NexusBuildPro.Editor.Data;
using NexusBuildPro.Editor.Profiling;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using BuildResult_Unity = UnityEditor.Build.Reporting.BuildResult;

namespace NexusBuildPro.Editor.Core
{
    /// <summary>
    /// Central orchestrator for the NexusBuildPro pipeline.
    /// Coordinates strategies, steps, cache, and profiling.
    /// </summary>
    public sealed class BuildOrchestrator
    {
        #region Fields
        private readonly List<IBuildStep> _steps = new();
        private readonly BuildCacheManager _cache;
        private readonly BuildProfiler _profiler;
        private readonly BuildHistoryData _history;
        private CancellationTokenSource _cts;
        #endregion

        #region Events
        public event Action<float, string> OnProgressChanged;
        public event Action<BuildLogEntry> OnLogEntry;
        public event Action<BuildResult> OnBuildCompleted;
        public event Action OnBuildStarted;
        public event Action OnBuildCancelled;
        #endregion

        #region Properties
        public bool IsBuilding { get; private set; }
        public BuildContext ActiveContext { get; private set; }
        public BuildProfiler Profiler => _profiler;
        #endregion

        #region Constructor
        public BuildOrchestrator()
        {
            _cache = new BuildCacheManager();
            _profiler = new BuildProfiler();
            _history = BuildHistoryData.Load();
        }
        #endregion

        #region Public Methods
        /// <summary>Registers a build step into the pipeline (sorted by Order).</summary>
        public void AddStep(IBuildStep step)
        {
            _steps.Add(step);
            _steps.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        public void RemoveStep<T>() where T : IBuildStep =>
            _steps.RemoveAll(s => s is T);

        /// <summary>Executes the full build pipeline for the given profile and strategy.</summary>
        public async Task<BuildResult> BuildAsync(BuildProfile profile, IPlatformBuildStrategy strategy)
        {
            if (IsBuilding)
            {
                Debug.LogWarning("[NexusBuildPro] A build is already in progress.");
                return null;
            }

            _cts = new CancellationTokenSource();
            var context = new BuildContext(profile, strategy, _cts.Token);
            ActiveContext = context;
            IsBuilding = true;

            SubscribeContextEvents(context);
            OnBuildStarted?.Invoke();
            _profiler.Begin(context.SessionId);
            _cache.BeginSession();

            var startTime = DateTime.UtcNow;
            BuildResult result = null;

            try
            {
                context.LogInfo($"=== NexusBuildPro Build Started ===");
                context.LogInfo($"Profile: {profile.ProfileName} | Platform: {strategy.PlatformName}");
                context.LogInfo($"Session: {context.SessionId}");

                // Validate platform module
                if (!strategy.IsModuleInstalled)
                {
                    context.LogError($"Build module for {strategy.PlatformName} is not installed.");
                    return BuildFailed(context, startTime, "Platform module not installed.", null);
                }

                // Validate configuration
                if (!strategy.ValidateConfiguration(context, out var validationError))
                {
                    context.LogError($"Configuration invalid: {validationError}");
                    return BuildFailed(context, startTime, validationError, null);
                }

                // Prepare output directory
                PrepareOutputDirectory(context);
                if (context.CancellationToken.IsCancellationRequested)
                    return BuildCancelled(context, startTime);

                // Run pre-build steps SYNCHRONOUSLY — no await before BuildPipeline.BuildPlayer
                // This ensures BuildPlayer is called in the same synchronous chain as delayCall,
                // which is outside Unity 6's player loop check.
                context.ReportProgress(0.05f, "Running pre-build steps");
                RunPipelineStepsSync(context, 0.05f, 0.40f);
                if (context.HasErrors)
                    return BuildFailed(context, startTime, "Pre-build step failed.", null);
                if (context.CancellationToken.IsCancellationRequested)
                    return BuildCancelled(context, startTime);

                // Configure build options via strategy
                context.ReportProgress(0.45f, "Configuring platform");
                strategy.PreparePlatform(context);
                var buildOptions = strategy.ConfigureBuildOptions(context);

                // Execute Unity build — still synchronous, no await has occurred yet
                context.ReportProgress(0.50f, $"Building for {strategy.PlatformName}...");
                _profiler.Metrics.BeginStep("UnityBuild");
                var report = BuildPipeline.BuildPlayer(buildOptions);
                float unityBuildTime = _profiler.Metrics.EndStep("UnityBuild");

                context.LogInfo($"Unity build completed in {unityBuildTime:F1}s");

                // Evaluate report
                bool success = report.summary.result == BuildResult_Unity.Succeeded;
                if (!success)
                {
                    var msg = $"Unity build failed: {report.summary.result}";
                    context.LogError(msg);
                    strategy.CleanupPlatform(context);
                    return BuildFailed(context, startTime, msg, report);
                }

                // Post-build steps are handled by PostBuildStep in the pre-build pipeline
                context.ReportProgress(0.85f, "Finalizing...");

                strategy.CleanupPlatform(context);
                context.ReportProgress(1.0f, "Build Complete");
                context.LogSuccess($"Build succeeded! Output: {buildOptions.locationPathName}");

                long outputSize = CalculateOutputSize(buildOptions.locationPathName);
                result = new BuildResult(
                    success: true,
                    platformId: strategy.PlatformId,
                    outputPath: buildOptions.locationPathName,
                    totalDuration: DateTime.UtcNow - startTime,
                    buildTime: startTime,
                    outputSizeBytes: outputSize,
                    errorMessage: null,
                    unityReport: report,
                    log: context.Log,
                    stepDurations: _profiler.Metrics.StepDurations,
                    sessionId: context.SessionId);

                RecordHistory(profile, strategy, result);
                _cache.CommitSession();
                OnBuildCompleted?.Invoke(result);
                return result;
            }
            catch (OperationCanceledException)
            {
                return BuildCancelled(context, startTime);
            }
            catch (Exception ex)
            {
                context.LogError($"Unexpected exception: {ex.Message}");
                Debug.LogException(ex);
                return BuildFailed(context, startTime, ex.Message, null);
            }
            finally
            {
                _profiler.End();
                IsBuilding = false;
                ActiveContext = null;
                UnsubscribeContextEvents(context);
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void CancelBuild()
        {
            if (!IsBuilding) return;
            _cts?.Cancel();
            OnBuildCancelled?.Invoke();
            Debug.Log("[NexusBuildPro] Build cancelled by user.");
        }

        public BuildHistoryData GetHistory() => _history;
        public BuildCacheManager GetCache() => _cache;
        #endregion

        #region Private Methods
        /// <summary>Runs all pipeline steps synchronously to avoid async SynchronizationContext
        /// resuming inside Unity 6's player loop before BuildPipeline.BuildPlayer.</summary>
        private void RunPipelineStepsSync(BuildContext context, float progressStart, float progressEnd)
        {
            var enabledSteps = _steps.Where(s => s.IsEnabled).ToList();
            if (enabledSteps.Count == 0) return;

            float stepRange = enabledSteps.Count > 0
                ? (progressEnd - progressStart) / enabledSteps.Count : 0f;

            for (int i = 0; i < enabledSteps.Count; i++)
            {
                if (context.CancellationToken.IsCancellationRequested) return;

                var step = enabledSteps[i];
                context.ReportProgress(progressStart + stepRange * i, step.StepName);
                context.LogInfo($"Running step: {step.StepName}");

                _profiler.Metrics.BeginStep(step.StepName);
                try
                {
                    // GetAwaiter().GetResult() runs the async step synchronously.
                    // Steps must NOT contain real async suspension (no Task.Yield / Task.Delay).
                    var stepResult = step.ExecuteAsync(context, context.CancellationToken)
                        .GetAwaiter().GetResult();
                    _profiler.Metrics.EndStep(step.StepName);

                    if (!stepResult.Success)
                    {
                        context.LogError($"Step '{step.StepName}' failed: {stepResult.Message}");
                        return;
                    }
                    context.LogInfo($"  ✓ {step.StepName} ({stepResult.DurationSeconds:F2}s)");
                }
                catch (Exception ex)
                {
                    _profiler.Metrics.EndStep(step.StepName);
                    context.LogError($"Step '{step.StepName}' threw: {ex.Message}");
                }
            }
        }

        private void PrepareOutputDirectory(BuildContext context)
        {
            var dir = context.Profile.ResolveOutputPath();
            if (string.IsNullOrEmpty(dir)) return;

            if (context.Profile.CleanBuildDirectory && Directory.Exists(dir))
            {
                context.LogInfo($"Cleaning output directory: {dir}");
                Directory.Delete(dir, recursive: true);
            }

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private long CalculateOutputSize(string outputPath)
        {
            try
            {
                if (File.Exists(outputPath)) return new FileInfo(outputPath).Length;
                if (Directory.Exists(outputPath))
                    return Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length);
            }
            catch { /* best effort */ }
            return 0;
        }

        private BuildResult BuildFailed(BuildContext context, DateTime start, string error, UnityEditor.Build.Reporting.BuildReport report)
        {
            var result = new BuildResult(false, context.Strategy.PlatformId,
                context.Profile.ResolveOutputPath(), DateTime.UtcNow - start, start,
                0, error, report, context.Log, _profiler.Metrics.StepDurations, context.SessionId);
            RecordHistory(context.Profile, context.Strategy, result);
            OnBuildCompleted?.Invoke(result);
            return result;
        }

        private BuildResult BuildCancelled(BuildContext context, DateTime start)
        {
            context.LogWarning("Build was cancelled.");
            return BuildFailed(context, start, "Cancelled by user.", null);
        }

        private void RecordHistory(BuildProfile profile, IPlatformBuildStrategy strategy, BuildResult result)
        {
            _history.AddEntry(new BuildHistoryEntry
            {
                SessionId = result.SessionId,
                ProfileName = profile.ProfileName,
                PlatformId = strategy.PlatformId,
                OutputPath = result.OutputPath,
                Success = result.Success,
                DurationSeconds = (float)result.TotalDuration.TotalSeconds,
                OutputSizeBytes = result.OutputSizeBytes,
                ErrorMessage = result.ErrorMessage,
                Timestamp = result.BuildTime.ToString("O"),
                Version = profile.Version
            });
            _history.Save();
        }

        private void SubscribeContextEvents(BuildContext context)
        {
            context.OnProgressChanged += (p, s) => OnProgressChanged?.Invoke(p, s);
            context.OnLogEntry += entry => OnLogEntry?.Invoke(entry);
        }

        private void UnsubscribeContextEvents(BuildContext context)
        {
            // Events are per-instance; context is discarded after build
        }
        #endregion
    }
}
#endif
