using System.Security.Cryptography;
using System.Text;

namespace WebHooks.Infrastructre.Security;

public static class WebhookSignature
{
    public static string ComputeSha256Hex(string secret, string signedPayload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(signedPayload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);

        return ConvertToHex(hash);
    }

    private static string ConvertToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}