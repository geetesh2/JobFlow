namespace JobFlow.Application.Models;

public sealed class JobExecutionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }

    public static JobExecutionResult Completed(TimeSpan duration) =>
        new() { Success = true, Duration = duration };

    public static JobExecutionResult Failed(string errorMessage, TimeSpan duration) =>
        new() { Success = false, ErrorMessage = errorMessage, Duration = duration };
}
