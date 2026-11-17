namespace GPMH.WebAPI.Controllers;

internal static class QueryParamHelper
{
    public static IReadOnlyCollection<Guid>? ParseGuidList(string[]? rawValues)
    {
        if (rawValues is null || rawValues.Length == 0)
        {
            return null;
        }

        List<Guid> parsed = [];
        foreach (string value in rawValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (string token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(token, out Guid guid) && guid != Guid.Empty)
                {
                    parsed.Add(guid);
                }
            }
        }

        return parsed.Count > 0 ? parsed : null;
    }

    public static string ResolveFrontendBaseUrl(HttpRequest request, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(configuration);

        string? configured = configuration["Frontend:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        string? origin = request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            return origin.TrimEnd('/');
        }

        return $"{request.Scheme}://{request.Host}".TrimEnd('/');
    }
}

