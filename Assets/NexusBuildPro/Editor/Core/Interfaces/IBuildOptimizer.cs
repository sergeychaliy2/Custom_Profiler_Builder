#if UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;

namespace NexusBuildPro.Editor.Core
{
    /// <summary>Interface for asset/code optimization passes.</summary>
    public interface IBuildOptimizer
    {
        string OptimizerName { get; }
        string Description { get; }
        OptimizerCategory Category { get; }
        bool IsEnabled { get; set; }

        Task<OptimizationResult> OptimizeAsync(BuildContext context, CancellationToken cancellationToken);
        float EstimateSavingsPercent(BuildContext context);
    }

    public enum OptimizerCategory { Textures, Audio, Meshes, Scripts, Shaders, AssetBundles }

    public sealed class OptimizationResult
    {
        public bool Success { get; }
        public string Summary { get; }
        public long BytesSaved { get; }
        public float DurationSeconds { get; }
        public int AssetsProcessed { get; }

        public OptimizationResult(bool success, string summary, long bytesSaved, float duration, int assets)
        {
            Success = success;
            Summary = summary;
            BytesSaved = bytesSaved;
            DurationSeconds = duration;
            AssetsProcessed = assets;
        }
    }
}
#endif
