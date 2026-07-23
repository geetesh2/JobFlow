namespace JobFlow.Domain.Common.Events;

public record JobCreatedEvent(Guid JobId, string Name, string Priority) : IDomainEvent;
