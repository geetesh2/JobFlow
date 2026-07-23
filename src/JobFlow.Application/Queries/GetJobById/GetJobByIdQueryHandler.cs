using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Application.DTOs;
using MediatR;

namespace JobFlow.Application.Queries.GetJobById;

public sealed class GetJobByIdQueryHandler : IRequestHandler<GetJobByIdQuery, JobResponse?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetJobByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<JobResponse?> Handle(GetJobByIdQuery request, CancellationToken cancellationToken)
    {
        var job = await _unitOfWork.Jobs.GetByIdAsync(request.Id, cancellationToken);
        if (job is null)
            return null;

        return new JobResponse(
            job.Id, job.Name, job.Status.ToString(), job.Priority.ToString(),
            job.RetryCount, job.MaxRetries, job.ProgressPercentage,
            job.ErrorMessage, job.CreatedBy, job.CreatedAtUtc, job.UpdatedAtUtc, job.CompletedAtUtc);
    }
}
