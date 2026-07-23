using JobFlow.Application.Models;
using JobFlow.Domain.Entities;

namespace JobFlow.Worker.Execution;

public interface IJobExecutor
{
    Task<JobExecutionResult> ExecuteAsync(Job job, string? payload, CancellationToken ct);
}
