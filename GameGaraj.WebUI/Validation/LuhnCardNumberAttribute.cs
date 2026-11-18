using System.ComponentModel.DataAnnotations;

namespace GameGaraj.WebUI.Validation;

/// <summary>
/// Validates credit/debit card numbers using digit length (13-19) and the Luhn (Mod 10) checksum.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class LuhnCardNumberAttribute : ValidationAttribute
{
    public LuhnCardNumberAttribute()
        : base("Geçerli bir kart numarası giriniz.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return true; // [Required] handles empty checks
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length < 13 || digits.Length > 19)
        {
            return false;
        }

        return PassesLuhnCheck(digits);
    }

    private static bool PassesLuhnCheck(string digits)
    {
        var sum = 0;
        var alternate = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}
