using System.Collections.Concurrent;
using System.Reflection;
using PdfSharp.Fonts;

namespace GameGaraj.Invoice.API.Services;

/// <summary>
/// Resolves embedded DejaVu Sans TrueType fonts without relying on host OS fonts.
/// </summary>
public sealed class SystemFontResolver : IFontResolver
{
    private static readonly Assembly CurrentAssembly = typeof(SystemFontResolver).Assembly;
    private static readonly ConcurrentDictionary<string, byte[]> FontCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> StyleResourceNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bi"] = "GameGaraj.Invoice.API.Fonts.DejaVuSans-BoldOblique.ttf",
            ["b"]  = "GameGaraj.Invoice.API.Fonts.DejaVuSans-Bold.ttf",
            ["i"]  = "GameGaraj.Invoice.API.Fonts.DejaVuSans-Oblique.ttf",
            ["r"]  = "GameGaraj.Invoice.API.Fonts.DejaVuSans.ttf"
        };

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        var style = (bold, italic) switch
        {
            (true, true) => "bi",
            (true, false) => "b",
            (false, true) => "i",
            _ => "r"
        };

        return new FontResolverInfo($"{familyName}#{style}");
    }

    public byte[]? GetFont(string faceName)
    {
        var style = faceName.Split('#').ElementAtOrDefault(1) ?? "r";
        var resourceName = StyleResourceNames.GetValueOrDefault(style, StyleResourceNames["r"]);

        return FontCache.GetOrAdd(resourceName, static name =>
        {
            using var stream = CurrentAssembly.GetManifestResourceStream(name)
                ?? throw new FileNotFoundException($"Embedded font resource '{name}' was not found.");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        });
    }
}
