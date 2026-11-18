using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace GameGaraj.WebUI.Validation;

/// <summary>
/// Validates card expiration in MM/YY format and ensures the card has not expired.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CardExpirationAttribute : ValidationAttribute
{
    public CardExpirationAttribute()
        : base("Son kullanma tarihi AA/YY formatında olmalı ve geçmiş bir tarih olmamalıdır.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return true; // [Required] handles empty checks
        }

        var parts = raw.Trim().Split('/');
        if (parts.Length != 2 || parts[0].Length != 2 || parts[1].Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var twoDigitYear))
        {
            return false;
        }

        if (month < 1 || month > 12)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var fullYear = (now.Year / 100) * 100 + twoDigitYear;
        var lastDayOfExpiryMonth = new DateTime(fullYear, month, DateTime.DaysInMonth(fullYear, month), 23, 59, 59, DateTimeKind.Utc);

        return lastDayOfExpiryMonth >= now;
    }
}
