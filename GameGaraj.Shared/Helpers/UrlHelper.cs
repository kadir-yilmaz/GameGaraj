using System.Text.RegularExpressions;
using System.Text;

namespace GameGaraj.Shared.Helpers
{
    public static class UrlHelper
    {
        public static string ToSlug(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = text.ToLowerInvariant();

            // Türkçe karakterleri dönüştür
            var stringBuilder = new StringBuilder();
            foreach (var c in text)
            {
                switch (c)
                {
                    case 'ı': stringBuilder.Append('i'); break;
                    case 'ğ': stringBuilder.Append('g'); break;
                    case 'ü': stringBuilder.Append('u'); break;
                    case 'ş': stringBuilder.Append('s'); break;
                    case 'ö': stringBuilder.Append('o'); break;
                    case 'ç': stringBuilder.Append('c'); break;
                    default: stringBuilder.Append(c); break;
                }
            }
            text = stringBuilder.ToString();

            // Geçersiz karakterleri temizle (sadece harf, rakam ve boşluk kalsın)
            text = Regex.Replace(text, @"[^a-z0-9\s-]", "");

            // Birden fazla boşluğu teke indir
            text = Regex.Replace(text, @"\s+", " ").Trim();

            // Boşlukları tireye çevir
            text = text.Replace(" ", "-");

            // Birden fazla tireyi teke indir
            text = Regex.Replace(text, @"-+", "-");

            return text;
        }

        public static string GenerateSlug(string name)
        {
            return ToSlug(name);
        }

        public static string GenerateSlug(string brand, string name)
        {
            var combined = string.IsNullOrWhiteSpace(brand) ? name : $"{brand} {name}";
            return ToSlug(combined);
        }
    }
}
