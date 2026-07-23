using JobFlow.Domain.Entities;
using JobFlow.Worker.Progress;
using Microsoft.Extensions.Logging;

namespace JobFlow.Worker.Handlers;

public sealed class DataProcessingJobHandler : IJobHandler
{
    private readonly IProgressReporter _progressReporter;
    private readonly ILogger<DataProcessingJobHandler> _logger;

    public DataProcessingJobHandler(IProgressReporter progressReporter, ILogger<DataProcessingJobHandler> logger)
    {
        _progressReporter = progressReporter;
        _logger = logger;
    }

    public string JobType => "DataProcessing";

    public async Task HandleAsync(Job job, string? payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting data processing job {JobId}.", job.Id);

        for (var i = 1; i <= 10; i++)
        {
            await Task.Delay(500, cancellationToken);
            var progress = i * 10;
            await _progressReporter.ReportProgressAsync(job.Id, progress, cancellationToken);
            _logger.LogInformation("Job {JobId} batch {Batch}/10 complete ({Progress}%).", job.Id, i, progress);
        }

        _logger.LogInformation("Data processing job {JobId} completed.", job.Id);
    }
}
