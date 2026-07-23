namespace JobFlow.Domain.Common.Events;

public record JobCompletedEvent(Guid JobId, DateTime CompletedAtUtc) : IDomainEvent;
