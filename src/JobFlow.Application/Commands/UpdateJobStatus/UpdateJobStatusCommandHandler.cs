using JobFlow.Application.Abstractions.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JobFlow.Application.Commands.UpdateJobStatus;

public sealed class UpdateJobStatusCommandHandler : IRequestHandler<UpdateJobStatusCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateJobStatusCommandHandler> _logger;

    public UpdateJobStatusCommandHandler(IUnitOfWork unitOfWork, ILogger<UpdateJobStatusCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(UpdateJobStatusCommand request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.Jobs.GetByIdAsync(request.JobId, cancellationToken);
        if (job is null)
        {
            _logger.LogWarning("Job {JobId} not found for status update.", request.JobId);
            return false;
        }

        try
        {
            switch (request.Status.ToLowerInvariant())
            {
                case "processing":
                    job.MarkAsProcessing();
                    break;
                case "completed":
                    job.MarkAsCompleted();
                    break;
                case "failed":
                    job.MarkAsFailed();
                    break;
                default:
                    _logger.LogWarning("Invalid status transition '{Status}' for job {JobId}.", request.Status, request.JobId);
                    return false;
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid state transition for job {JobId}: {Message}", request.JobId, ex.Message);
            return false;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
