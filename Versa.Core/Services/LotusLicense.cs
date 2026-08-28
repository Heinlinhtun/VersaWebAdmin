using System.Security.Cryptography;
using System.Text;

namespace Versa.Core.Services;

public static class LotusLicense
{
    public static string GenerateActivationKey(string deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
            throw new ArgumentException("Device code is required.", nameof(deviceCode));

        var normalizedDeviceCode = deviceCode.Trim().ToUpperInvariant();
        var raw = $"lotus-pos-full:{normalizedDeviceCode}:versa-retail-2026";
        var hash = Sha256Hex(raw);

        return string.Join("-", Enumerable.Range(0, 4)
            .Select(i => hash.Substring(i * 5, 5)))
            .ToUpperInvariant();
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
