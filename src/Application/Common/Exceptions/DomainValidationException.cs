namespace GPMH.Application.Common.Exceptions;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    public DomainValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    public DomainValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    public DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation failures have occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

