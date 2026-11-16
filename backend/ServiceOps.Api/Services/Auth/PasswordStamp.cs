using System.Security.Cryptography;
using System.Text;
namespace ServiceOps.Api.Services.Auth;

public static class PasswordStamp
{
    // Signed fingerprint, never the password hash itself. Changing the hash invalidates existing JWTs.
    public static string Create(string hash, string signingKey) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes("ServiceOps:password-version:" + hash)));
    public static bool Valid(string? stamp, string hash, string signingKey)
    {
        if (stamp == null || stamp.Length != 64) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stamp), Encoding.ASCII.GetBytes(Create(hash, signingKey)));
    }
}
