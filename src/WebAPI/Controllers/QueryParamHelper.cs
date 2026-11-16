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
}

