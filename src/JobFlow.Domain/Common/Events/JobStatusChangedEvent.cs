using JobFlow.Domain.Enums;

namespace JobFlow.Domain.Common.Events;

public record JobStatusChangedEvent(Guid JobId, JobStatus OldStatus, JobStatus NewStatus) : IDomainEvent;
