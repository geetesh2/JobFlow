using JobFlow.Worker.Configuration;
using Microsoft.Extensions.Options;

namespace JobFlow.Worker.Retry;

public sealed class ExponentialRetryPolicy : IJobRetryPolicy
{
    private readonly WorkerOptions _options;
    private readonly Random _jitterRandom = new();

    public ExponentialRetryPolicy(IOptions<WorkerOptions> options)
    {
        _options = options.Value;
    }

    public TimeSpan GetDelay(int attemptNumber)
    {
        var baseDelay = _options.RetryBaseDelaySeconds * Math.Pow(2, attemptNumber);
        var jitter = _jitterRandom.NextDouble(); // 0-1 second jitter
        return TimeSpan.FromSeconds(baseDelay + jitter);
    }

    public bool ShouldRetry(int attemptNumber, Exception exception)
    {
        if (exception is OperationCanceledException)
            return false;

        return attemptNumber < _options.MaxRetries;
    }
}
