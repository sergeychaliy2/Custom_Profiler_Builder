#if UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;

namespace NexusBuildPro.Editor.Core
{
    /// <summary>Represents a single atomic step in the build pipeline.</summary>
    public interface IBuildStep
    {
        string StepName { get; }
        int Order { get; }
        bool IsEnabled { get; set; }

        Task<StepResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken);
    }

    public sealed class StepResult
    {
        public bool Success { get; }
        public string Message { get; }
        public float DurationSeconds { get; }

        public StepResult(bool success, string message, float duration)
        {
            Success = success;
            Message = message;
            DurationSeconds = duration;
        }

        public static StepResult Ok(string message, float duration) => new StepResult(true, message, duration);
        public static StepResult Fail(string message, float duration) => new StepResult(false, message, duration);
    }
}
#endif
