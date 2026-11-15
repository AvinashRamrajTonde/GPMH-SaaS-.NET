namespace GPMH.Infrastructure.Services.LgdDirectory;

public sealed class LgdDirectoryOptions
{
    public const string SectionName = "ExternalServices";

    /// <summary>
    /// Base URL for the Maharashtra GP Directory REST API. Must end with '/' so
    /// HttpClient preserves the /api/v1/ path prefix on relative requests.
    /// </summary>
    public string LgdApiBaseUrl { get; set; } = "https://api.grampanchayatmh.in/api/v1/";
}

