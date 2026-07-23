using System.Text.Json;
using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Contracts.Messages;
using JobFlow.Domain.Entities;
using JobFlow.Domain.Enums;
using JobFlow.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JobFlow.Application.Commands.CreateJob;

public sealed class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, Guid>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<CreateJobCommandHandler> _logger;

    public CreateJobCommandHandler(
        IUnitOfWork unitOfWork,
        IApplicationDbContext dbContext,
        ILogger<CreateJobCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateJobCommand request, CancellationToken cancellationToken)
    {
        var priority = Enum.TryParse<JobPriority>(request.Priority, true, out var p) ? p : JobPriority.Normal;
        var metadata = new JobMetadata(request.Tags, request.Source);

        var job = new Job(request.Name, priority, request.Payload, request.MaxRetries, request.CreatedBy, metadata);

        await _unitOfWork.Jobs.AddAsync(job, cancellationToken);

        var message = new JobCreatedMessage(
            job.Id,
            job.Name,
            job.Priority.ToString(),
            request.Payload,
            job.MaxRetries,
            DateTime.UtcNow,
            Guid.NewGuid().ToString("N"),
            System.Diagnostics.Activity.Current?.Id);

        var outboxMessage = new OutboxMessage(
            nameof(JobCreatedMessage),
            JsonSerializer.Serialize(message));

        _dbContext.OutboxMessages.Add(outboxMessage);

        // Single SaveChangesAsync commits Job + OutboxMessage atomically
        // UoW also dispatches domain events (JobCreatedEvent -> Mongo/ES sync)
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return job.Id;
    }
}
