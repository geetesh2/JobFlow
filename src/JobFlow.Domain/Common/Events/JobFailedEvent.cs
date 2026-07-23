namespace JobFlow.Domain.Common.Events;

public record JobFailedEvent(Guid JobId, string? ErrorMessage) : IDomainEvent;
