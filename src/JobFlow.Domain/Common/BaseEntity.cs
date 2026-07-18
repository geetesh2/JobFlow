using JobFlow.Domain.Common.Events;

namespace JobFlow.Domain.Common;

public abstract class BaseEntity<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; protected set; } = default!;

    public DateTime CreatedAtUtc { get; protected set; }

    public DateTime UpdatedAtUtc { get; protected set; }

    public void SetCreatedAtUtc(DateTime createdAtUtc)
    {
        CreatedAtUtc = createdAtUtc;
    }

    public void SetUpdatedAtUtc(DateTime updatedAtUtc)
    {
        UpdatedAtUtc = updatedAtUtc;
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents =>
        _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
