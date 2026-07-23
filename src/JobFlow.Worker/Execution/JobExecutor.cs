using System.Diagnostics;
using JobFlow.Application.Models;
using JobFlow.Domain.Entities;
using JobFlow.Worker.Configuration;
using JobFlow.Worker.Handlers;
using JobFlow.Worker.Progress;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobFlow.Worker.Execution;

public sealed class JobExecutor : IJobExecutor
{
    private readonly IEnumerable<IJobHandler> _handlers;
    private readonly IProgressReporter _progressReporter;
    private readonly ILogger<JobExecutor> _logger;
    private readonly WorkerOptions _options;

    public JobExecutor(
        IEnumerable<IJobHandler> handlers,
        IProgressReporter progressReporter,
        ILogger<JobExecutor> logger,
        IOptions<WorkerOptions> options)
    {
        _handlers = handlers;
        _progressReporter = progressReporter;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<JobExecutionResult> ExecuteAsync(Job job, string? payload, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.JobTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var token = linkedCts.Token;

        try
        {
            var handler = _handlers.FirstOrDefault(h => h.JobType == job.Name)
                          ?? _handlers.FirstOrDefault(h => h.JobType == "Default");

            if (handler is not null)
            {
                _logger.LogInformation("Executing job {JobId} with handler {HandlerType}.", job.Id, handler.GetType().Name);
                await handler.HandleAsync(job, payload, token);
            }
            else
            {
                _logger.LogInformation("No handler found for job {JobId} ({Name}). Running default processing.", job.Id, job.Name);
                await RunDefaultProcessingAsync(job, token);
            }

            sw.Stop();
            return JobExecutionResult.Completed(sw.Elapsed);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            sw.Stop();
            var message = $"Job {job.Id} timed out after {_options.JobTimeoutSeconds}s.";
            _logger.LogWarning(message);
            return JobExecutionResult.Failed(message, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Job {JobId} execution failed.", job.Id);
            return JobExecutionResult.Failed(ex.Message, sw.Elapsed);
        }
    }

    private async Task RunDefaultProcessingAsync(Job job, CancellationToken ct)
    {
        var steps = new[] { 25, 50, 75, 100 };
        foreach (var pct in steps)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1250), ct);
            await _progressReporter.ReportProgressAsync(job.Id, pct, ct);
        }
    }
}
