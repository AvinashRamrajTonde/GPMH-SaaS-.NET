using System.Text.Json;
using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Common;
using GPMH.Domain.Entities.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GPMH.Infrastructure.Auditing;

/// <summary>
/// Same-transaction EF Core interceptor enforcing tenant stamping, UTC timestamps,
/// and append-only field-level JSON diff AuditLog entries with PII/secret masking.
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider)
    : SaveChangesInterceptor
{
    private static readonly string[] SensitiveKeywords =
    [
        "password", "secret", "token", "aadhaar", "aadhar", "pan", "salt", "apikey", "hash", "encrypted",
    ];

    private static readonly HashSet<string> IgnoredDiffColumns = new(StringComparer.Ordinal)
    {
        "ConcurrencyStamp",
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ApplyAuditAndTenancy(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ApplyAuditAndTenancy(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditAndTenancy(DbContext context)
    {
        DateTimeOffset now = dateTimeProvider.UtcNow;
        List<AuditLog> pendingLogs = [];

        // Materialize snapshot before adding AuditLog rows so we never mutate active ChangeTracker enumeration.
        List<EntityEntry> entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog &&
                        e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (EntityEntry entry in entries)
        {
            if (entry.Entity is ITenantEntity tenantEntity &&
                entry.State == EntityState.Added &&
                tenantEntity.TenantId == Guid.Empty &&
                tenantContext.CurrentTenantId is Guid resolvedTenantId)
            {
                tenantEntity.TenantId = resolvedTenantId;
            }

            if (entry.Entity is BaseEntity baseEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    baseEntity.CreatedAtUtc = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    baseEntity.UpdatedAtUtc = now;
                }
            }

            pendingLogs.Add(BuildAuditLog(entry, now));
        }

        if (pendingLogs.Count > 0)
        {
            context.Set<AuditLog>().AddRange(pendingLogs);
        }
    }

    private AuditLog BuildAuditLog(EntityEntry entry, DateTimeOffset timestampUtc)
    {
        Dictionary<string, object?> oldValues = new(StringComparer.Ordinal);
        Dictionary<string, object?> newValues = new(StringComparer.Ordinal);
        List<string> changedColumns = [];

        foreach (PropertyEntry prop in entry.Properties)
        {
            string propName = prop.Metadata.Name;
            if (IgnoredDiffColumns.Contains(propName))
            {
                continue;
            }

            bool isSensitive = IsSensitiveProperty(propName);

            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[propName] = MaskIfSensitive(prop.CurrentValue, isSensitive);
                    break;

                case EntityState.Deleted:
                    oldValues[propName] = MaskIfSensitive(prop.OriginalValue, isSensitive);
                    break;

                case EntityState.Modified when prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue):
                    changedColumns.Add(propName);
                    oldValues[propName] = MaskIfSensitive(prop.OriginalValue, isSensitive);
                    newValues[propName] = MaskIfSensitive(prop.CurrentValue, isSensitive);
                    break;
            }
        }

        string action = ResolveAuditAction(entry);

        Guid? entityTenantId = entry.Entity is ITenantEntity t && t.TenantId != Guid.Empty
            ? t.TenantId
            : tenantContext.CurrentTenantId;

        string entityId = entry.Properties
            .FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString()
            ?? string.Empty;

        return new AuditLog
        {
            TenantId = entityTenantId,
            EntityName = entry.Metadata.ClrType.Name,
            EntityId = entityId,
            Action = action,
            ActorId = currentUserService.UserId?.ToString(),
            ActorRole = currentUserService.Role?.ToString(),
            CorrelationId = currentUserService.CorrelationId,
            IpAddress = currentUserService.IpAddress,
            UserAgent = currentUserService.UserAgent,
            OldValuesJson = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null,
            NewValuesJson = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null,
            ChangedColumnsJson = changedColumns.Count > 0 ? JsonSerializer.Serialize(changedColumns) : null,
            TimestampUtc = timestampUtc,
        };
    }

    private static string ResolveAuditAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Modified)
        {
            PropertyEntry? isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
            if (isDeletedProp is not null
                && isDeletedProp.IsModified
                && isDeletedProp.OriginalValue is false
                && isDeletedProp.CurrentValue is true)
            {
                return "SoftDeleted";
            }
        }

        return entry.State.ToString();
    }

    private static bool IsSensitiveProperty(string propertyName)
    {
        foreach (string keyword in SensitiveKeywords)
        {
            if (propertyName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static object? MaskIfSensitive(object? value, bool isSensitive) =>
        isSensitive && value is not null ? "[MASKED]" : value;
}

