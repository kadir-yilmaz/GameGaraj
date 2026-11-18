using PdfSharp.Fonts;

namespace GameGaraj.Invoice.API.Services;

/// <summary>
/// Resolves system TrueType fonts across Linux containers and Windows environments with safe fallbacks.
/// </summary>
public sealed class SystemFontResolver : IFontResolver
{
    private static readonly IReadOnlyDictionary<string, string[]> StyleFontCandidates =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["bi"] = ["DejaVuSans-BoldOblique.ttf", "DejaVuSans-Bold.ttf", "DejaVuSans.ttf", "arialbi.ttf", "segoeuiz.ttf"],
            ["b"]  = ["DejaVuSans-Bold.ttf", "DejaVuSans.ttf", "arialbd.ttf", "segoeuib.ttf", "tahomabd.ttf"],
            ["i"]  = ["DejaVuSans-Oblique.ttf", "DejaVuSans.ttf", "ariali.ttf", "segoeuii.ttf"],
            ["r"]  = ["DejaVuSans.ttf", "FreeSans.ttf", "arial.ttf", "segoeui.ttf", "tahoma.ttf"]
        };

    private static readonly string[] SearchDirectories = BuildSearchDirectories();

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
        var candidates = StyleFontCandidates.GetValueOrDefault(style, StyleFontCandidates["r"]);

        foreach (var dir in SearchDirectories)
        {
            foreach (var fileName in candidates)
            {
                var fullPath = Path.Combine(dir, fileName);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllBytes(fullPath);
                }
            }
        }

        throw new FileNotFoundException($"No suitable TrueType font found for face '{faceName}'.");
    }

    private static string[] BuildSearchDirectories()
    {
        var dirs = new List<string>
        {
            "/usr/share/fonts/truetype/dejavu",
            "/usr/share/fonts/dejavu",
            "/usr/share/fonts/truetype/freefont",
            "/usr/share/fonts/truetype",
            "/usr/share/fonts"
        };

        var systemFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrWhiteSpace(systemFonts))
        {
            dirs.Add(systemFonts);
        }

        return dirs.Where(Directory.Exists).Distinct().ToArray();
    }
}
