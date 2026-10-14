using System.Security.Cryptography;
using System.Text;

namespace PettyCashDatabase.Web.Authentication;

public sealed class PasswordHasher
{
    private readonly byte[] _pepper;
    private readonly byte[] _rowSignKey;

    public PasswordHasher(IConfiguration configuration)
    {
        _pepper = Encoding.UTF8.GetBytes(RequireKey(configuration, "PETTYCASH_PEPPER_KEY"));
        _rowSignKey = Encoding.UTF8.GetBytes(RequireKey(configuration, "PETTYCASH_ROWSIGN_KEY"));
    }

    public string HashPassword(string password, int userId)
    {
        var input = BuildPepperedInput(password, userId);
        return BCrypt.Net.BCrypt.EnhancedHashPassword(input, workFactor: 12);
    }

    public bool VerifyPassword(string password, int userId, string passwordHash)
    {
        var input = BuildPepperedInput(password, userId);
        return BCrypt.Net.BCrypt.EnhancedVerify(input, passwordHash);
    }

    /// <summary>Timing-safe dummy verify when no user exists.</summary>
    public void DummyVerify(string password)
    {
        const string dummyHash = "$2a$12$K3xqJ8vH5nY0mR2pL4wQ.eO9sT1uV6xY7zA8bC0dE1fG2hI3jK4lM";
        try
        {
            BCrypt.Net.BCrypt.EnhancedVerify(BuildPepperedInput(password, 0), dummyHash);
        }
        catch
        {
            // Intentional: absorb verification cost only.
        }
    }

    public string ComputeRowSignature(int userId, string email, string passwordHash)
    {
        var data = $"{userId}|{email}|{passwordHash}";
        var hash = HMACSHA256.HashData(_rowSignKey, Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    public bool VerifyRowSignature(int userId, string email, string passwordHash, string? rowSignature)
    {
        if (string.IsNullOrEmpty(rowSignature))
            return false;

        var expected = ComputeRowSignature(userId, email, passwordHash);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(rowSignature);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string BuildPepperedInput(string password, int userId)
    {
        var data = $"{password}:{userId}";
        var mac = HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(mac);
    }

    private static string RequireKey(IConfiguration configuration, string envName)
    {
        var value = Environment.GetEnvironmentVariable(envName) ?? configuration[envName];
        if (string.IsNullOrWhiteSpace(value) || value.Length < 32)
            throw new InvalidOperationException(
                $"{envName} must be set and at least 32 characters. Use env vars or dotnet user-secrets.");

        return value;
    }
}
