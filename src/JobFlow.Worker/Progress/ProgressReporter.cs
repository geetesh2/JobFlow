using JobFlow.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobFlow.Worker.Progress;

public sealed class ProgressReporter : IProgressReporter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProgressReporter> _logger;

    public ProgressReporter(IServiceScopeFactory scopeFactory, ILogger<ProgressReporter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ReportProgressAsync(Guid jobId, int percentage, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var job = await dbContext.Jobs.FindAsync(new object[] { jobId }, ct);
        if (job is null)
        {
            _logger.LogWarning("Job {JobId} not found when reporting progress.", jobId);
            return;
        }

        job.UpdateProgress(percentage);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Job {JobId} progress updated to {Percentage}%.", jobId, percentage);
    }
}
