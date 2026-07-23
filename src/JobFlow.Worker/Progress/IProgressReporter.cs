namespace JobFlow.Worker.Progress;

public interface IProgressReporter
{
    Task ReportProgressAsync(Guid jobId, int percentage, CancellationToken ct = default);
}
