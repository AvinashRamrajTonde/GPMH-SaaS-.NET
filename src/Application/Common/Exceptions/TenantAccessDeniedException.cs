namespace GPMH.Application.Common.Exceptions;

public sealed class TenantAccessDeniedException : Exception
{
    public TenantAccessDeniedException()
        : base("Access denied for the requested Gram Panchayat tenant.")
    {
    }

    public TenantAccessDeniedException(string message)
        : base(message)
    {
    }

    public TenantAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

