namespace JobFlow.Worker.Retry;

public interface IJobRetryPolicy
{
    TimeSpan GetDelay(int attemptNumber);
    bool ShouldRetry(int attemptNumber, Exception exception);
}
