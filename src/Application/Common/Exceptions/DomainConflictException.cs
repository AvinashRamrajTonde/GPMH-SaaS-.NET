namespace GPMH.Application.Common.Exceptions;

public sealed class DomainConflictException : Exception
{
    public string ErrorCode { get; }
    public IReadOnlyDictionary<string, object?> Metadata { get; }

    public DomainConflictException(
        string errorCode,
        string message,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Metadata = metadata ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }
}

