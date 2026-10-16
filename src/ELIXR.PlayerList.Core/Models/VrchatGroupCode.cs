using System.Text.RegularExpressions;
namespace ELIXR.PlayerList.Core.Models;

public static class VrchatGroupCode
{
    public static bool IsValid(string? value) => value is not null && Regex.IsMatch(value, @"\A[A-Z0-9]{3,6}\.[0-9]{4}\z");
    public static string? FromParts(string shortCode, string discriminator)
    {
        var value = shortCode.Trim().ToUpperInvariant() + "." + discriminator.Trim();
        return IsValid(value) ? value : null;
    }
}
