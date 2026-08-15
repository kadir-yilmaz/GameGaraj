using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace GameGaraj.WebUI.Extensions
{
    /// <summary>
    /// Bidirectional Base62 encoder/decoder for converting 128-bit GUIDs into clean, URL-safe alphanumeric strings.
    /// Example: "36e7e2bc-d55e-404e-9115-9cb59d3837d5" ↔ "1qX7r2k9L5mP0aBc4DeF6G"
    /// </summary>
    public static class Base62Helper
    {
        private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        public static string Encode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            if (Guid.TryParse(input, out var guid))
            {
                return EncodeGuid(guid);
            }

            return input;
        }

        public static string Decode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // If already a valid GUID with or without hyphens
            if (Guid.TryParse(input, out var directGuid))
            {
                return directGuid.ToString();
            }

            if (input.Length <= 24 && TryDecodeGuid(input, out var guid))
            {
                return guid.ToString();
            }

            return input;
        }

        public static string EncodeGuid(Guid guid)
        {
            var bytes = guid.ToByteArray();
            var positiveBytes = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, positiveBytes, 0, bytes.Length);
            var bigInt = new BigInteger(positiveBytes);

            if (bigInt.IsZero)
                return "0";

            var sb = new StringBuilder();
            while (bigInt > 0)
            {
                bigInt = BigInteger.DivRem(bigInt, 62, out var rem);
                sb.Append(Base62Chars[(int)rem]);
            }

            return sb.ToString();
        }

        public static bool TryDecodeGuid(string base62, out Guid guid)
        {
            guid = Guid.Empty;
            if (string.IsNullOrWhiteSpace(base62))
                return false;

            try
            {
                var bigInt = BigInteger.Zero;
                var multiplier = BigInteger.One;

                for (int i = 0; i < base62.Length; i++)
                {
                    int digit = Base62Chars.IndexOf(base62[i]);
                    if (digit < 0) return false;
                    bigInt += digit * multiplier;
                    multiplier *= 62;
                }

                var bytes = bigInt.ToByteArray();
                var guidBytes = new byte[16];
                Buffer.BlockCopy(bytes, 0, guidBytes, 0, Math.Min(bytes.Length, 16));
                guid = new Guid(guidBytes);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Parses and builds Hepsiburada-style SEO slugs using Base62 IDs.
    /// Product:  {slug}-p-{base62Id}   e.g.  samsung-galaxy-s24-ultra-p-1qX7r2k9L5mP0aBc4DeF6G
    /// Category: {slug}-c-{base62Id}  e.g.  ram-c-34kL89mPqR5stUvWxYz01A
    /// </summary>
    public static class SlugHelper
    {
        // Matches {slug}-p-{alphanumeric/hyphenated ID}
        private static readonly Regex ProductPattern = new(@"^(.+)-p-([a-zA-Z0-9\-]+)$", RegexOptions.Compiled);
        // Matches {slug}-c-{alphanumeric/hyphenated ID}
        private static readonly Regex CategoryPattern = new(@"^(.+)-c-([a-zA-Z0-9\-]+)$", RegexOptions.Compiled);

        public static bool IsProductSlug(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && ProductPattern.IsMatch(value);
        }

        public static bool IsCategorySlug(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && CategoryPattern.IsMatch(value);
        }

        /// <summary>
        /// Parses "samsung-galaxy-s24-p-1qX7r2k9L5mP" → (slug: "samsung-galaxy-s24", id: "36e7e2bc-d55e-...")
        /// </summary>
        public static (string slug, string id) ParseProductSlug(string compositeSlug)
        {
            var match = ProductPattern.Match(compositeSlug);
            if (!match.Success)
                return (compositeSlug, string.Empty);

            var rawId = match.Groups[2].Value;
            var decodedId = Base62Helper.Decode(rawId);
            return (match.Groups[1].Value, decodedId);
        }

        /// <summary>
        /// Parses "ram-c-1qX7r2k9L5mP" → (slug: "ram", id: "36e7e2bc-d55e-...")
        /// </summary>
        public static (string slug, string id) ParseCategorySlug(string compositeSlug)
        {
            var match = CategoryPattern.Match(compositeSlug);
            if (!match.Success)
                return (compositeSlug, string.Empty);

            var rawId = match.Groups[2].Value;
            var decodedId = Base62Helper.Decode(rawId);
            return (match.Groups[1].Value, decodedId);
        }

        /// <summary>
        /// Builds a product detail URL: /samsung-galaxy-s24-p-1qX7r2k9L5mP
        /// </summary>
        public static string BuildProductUrl(string? slug, string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "/ara";

            var cleanSlug = !string.IsNullOrWhiteSpace(slug) ? slug : "urun";
            var encodedId = Base62Helper.Encode(id);
            return $"/{cleanSlug}-p-{encodedId}";
        }

        /// <summary>
        /// Builds a category URL: /ram-c-1qX7r2k9L5mP
        /// </summary>
        public static string BuildCategoryUrl(string? slug, string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "/ara";

            var cleanSlug = !string.IsNullOrWhiteSpace(slug) ? slug : "kategori";
            var encodedId = Base62Helper.Encode(id);
            return $"/{cleanSlug}-c-{encodedId}";
        }

        /// <summary>
        /// Builds an SEO search URL: /ara?q=corsair+k100+rgb
        /// </summary>
        public static string BuildSearchUrl(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "/ara";

            var encoded = Uri.EscapeDataString(query.Trim()).Replace("%20", "+");
            return $"/ara?q={encoded}";
        }

        /// <summary>
        /// Converts arbitrary text to URL slug (Turkish character support)
        /// </summary>
        public static string ToSlug(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "urun";

            text = text.ToLowerInvariant()
                .Replace("ı", "i")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ş", "s")
                .Replace("ö", "o")
                .Replace("ç", "c");

            // Remove invalid characters
            text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
            // Replace multiple spaces/hyphens with single hyphen
            text = Regex.Replace(text, @"[\s-]+", "-").Trim('-');

            return string.IsNullOrWhiteSpace(text) ? "urun" : text;
        }
    }

    /// <summary>
    /// Route constraint that matches product slugs containing "-p-".
    /// </summary>
    public class ProductSlugConstraint : IRouteConstraint
    {
        public bool Match(HttpContext? httpContext, IRouter? route, string routeKey,
            RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (!values.TryGetValue(routeKey, out var value) || value == null)
                return false;

            return SlugHelper.IsProductSlug(value.ToString());
        }
    }

    /// <summary>
    /// Route constraint that matches category slugs containing "-c-".
    /// </summary>
    public class CategorySlugConstraint : IRouteConstraint
    {
        public bool Match(HttpContext? httpContext, IRouter? route, string routeKey,
            RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (!values.TryGetValue(routeKey, out var value) || value == null)
                return false;

            return SlugHelper.IsCategorySlug(value.ToString());
        }
    }
}
