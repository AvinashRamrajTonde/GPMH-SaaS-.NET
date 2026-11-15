using GPMH.Domain.Common;

namespace GPMH.Domain.Events;

public sealed record TenantOnboardedDomainEvent(
    Guid TenantId,
    string LgdCode,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

