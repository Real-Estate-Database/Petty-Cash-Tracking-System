using System.Security.Cryptography;
using System.Text;

namespace PettyCashDatabase.Web.Authentication;

/// <summary>
/// Provides cryptographically hardened password hashing (PBKDF2/BCrypt with pepper),
/// constant-time verification, and HMAC-SHA256 row signatures for tamper detection.
/// </summary>
public sealed class PasswordHasher
{
    private readonly byte[] _pepper;
    private readonly byte[] _rowSignKey;

    public PasswordHasher(IConfiguration configuration)
    {
        // 1. Load security keys from environment or user-secrets
        _pepper = Encoding.UTF8.GetBytes(RequireKey(configuration, "PETTYCASH_PEPPER_KEY"));
        _rowSignKey = Encoding.UTF8.GetBytes(RequireKey(configuration, "PETTYCASH_ROWSIGN_KEY"));
    }

    /// <summary>
    /// Computes an HMAC peppered BCrypt hash of the plaintext password.
    /// </summary>
    public string HashPassword(string password, int userId)
    {
        // 1. Pepper password with server secret and user ID
        var input = BuildPepperedInput(password, userId);

        // 2. Compute enhanced BCrypt hash with cost factor 12
        return BCrypt.Net.BCrypt.EnhancedHashPassword(input, workFactor: 12);
    }

    /// <summary>
    /// Verifies a plaintext password against the stored peppered BCrypt hash.
    /// </summary>
    public bool VerifyPassword(string password, int userId, string passwordHash)
    {
        // 1. Pepper candidate password
        var input = BuildPepperedInput(password, userId);

        // 2. Perform constant-time verification against stored hash
        return BCrypt.Net.BCrypt.EnhancedVerify(input, passwordHash);
    }

    /// <summary>
    /// Timing-safe dummy verification executed when an account does not exist
    /// to mitigate user enumeration through response-time discrepancies.
    /// </summary>
    public void DummyVerify(string password)
    {
        const string dummyHash = "$2a$12$K3xqJ8vH5nY0mR2pL4wQ.eO9sT1uV6xY7zA8bC0dE1fG2hI3jK4lM";
        try
        {
            // 1. Absorb standard verification CPU cost
            BCrypt.Net.BCrypt.EnhancedVerify(BuildPepperedInput(password, 0), dummyHash);
        }
        catch
        {
            // Intentional: absorb verification cost only.
        }
    }

    /// <summary>
    /// Computes a tamper-evident HMAC-SHA256 signature over critical user fields (UserId, Email, PasswordHash).
    /// </summary>
    public string ComputeRowSignature(int userId, string email, string passwordHash)
    {
        // 1. Serialize immutable boundary string
        var data = $"{userId}|{email}|{passwordHash}";

        // 2. Compute HMAC-SHA256 using server row-signing key
        var hash = HMACSHA256.HashData(_rowSignKey, Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Validates the stored row signature in constant time to detect direct database tampering.
    /// </summary>
    public bool VerifyRowSignature(int userId, string email, string passwordHash, string? rowSignature)
    {
        if (string.IsNullOrEmpty(rowSignature))
            return false;

        // 1. Compute expected signature
        var expected = ComputeRowSignature(userId, email, passwordHash);

        // 2. Compare bytes in constant time using FixedTimeEquals
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(rowSignature);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    /// <summary>
    /// Computes a standard SHA-256 hex string.
    /// </summary>
    public static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Generates an HMAC-SHA256 peppered input binding the plaintext password to the specific user ID.
    /// </summary>
    private string BuildPepperedInput(string password, int userId)
    {
        var data = $"{password}:{userId}";
        var mac = HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(mac);
    }

    /// <summary>
    /// Validates that required security configuration keys are loaded and meet length requirements.
    /// </summary>
    private static string RequireKey(IConfiguration configuration, string envName)
    {
        var value = Environment.GetEnvironmentVariable(envName) ?? configuration[envName];
        if (string.IsNullOrWhiteSpace(value) || value.Length < 32)
            throw new InvalidOperationException(
                $"{envName} must be set and at least 32 characters. Use env vars or dotnet user-secrets.");

        return value;
    }
}
