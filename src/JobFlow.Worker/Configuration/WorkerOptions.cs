namespace JobFlow.Worker.Configuration;

public sealed class WorkerOptions
{
    public int MaxConcurrency { get; set; } = 4;
    public int MaxRetries { get; set; } = 3;
    public int RetryBaseDelaySeconds { get; set; } = 2;
    public int JobTimeoutSeconds { get; set; } = 300;
    public string DlxExchange { get; set; } = "jobflow.dlx";
    public string DeadLetterQueue { get; set; } = "jobflow.dead-letter";
}
