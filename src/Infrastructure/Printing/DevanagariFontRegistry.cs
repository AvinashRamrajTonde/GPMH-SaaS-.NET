using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace GPMH.Infrastructure.Printing;

public static class DevanagariFontRegistry
{
    public const string PrimaryFontFamily = "Noto Sans Devanagari";
    public const string FallbackFontFamily = "Noto Sans";

    private static readonly Lock SyncRoot = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;

            Assembly asm = typeof(DevanagariFontRegistry).Assembly;
            string[] fontResources =
            [
                .. asm.GetManifestResourceNames()
                    .Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n, StringComparer.Ordinal),
            ];

            foreach (string resourceName in fontResources)
            {
                using Stream? stream = asm.GetManifestResourceStream(resourceName);
                if (stream is not null)
                {
                    FontManager.RegisterFontFromStream(stream);
                }
            }

            _initialized = true;
        }
    }
}
