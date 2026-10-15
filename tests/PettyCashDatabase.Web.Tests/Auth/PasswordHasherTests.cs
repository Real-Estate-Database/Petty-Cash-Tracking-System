using Microsoft.Extensions.Configuration;
using PettyCashDatabase.Web.Authentication;
using Xunit;

namespace PettyCashDatabase.Tests;

/// <summary>
/// Unit tests for PBKDF2/BCrypt peppered password hashing and tamper-evident row signature verification.
/// </summary>
public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher;

    public PasswordHasherTests()
    {
        // 1. Arrange configuration with 32+ character cryptographic keys
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["PETTYCASH_PEPPER_KEY"] = "test_pepper_key_of_at_least_32_characters_length_long!",
            ["PETTYCASH_ROWSIGN_KEY"] = "test_rowsign_key_of_at_least_32_characters_length_long!"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        _hasher = new PasswordHasher(configuration);
    }

    [Fact]
    public void HashPassword_And_VerifyPassword_ShouldSucceedForValidCredentials()
    {
        // 1. Arrange user identity and raw password
        const int userId = 10;
        const string rawPassword = "StrongPassword@2026!";

        // 2. Act: Hash password
        var hash = _hasher.HashPassword(rawPassword, userId);

        // 3. Assert: Verify hash validity and constant-time check
        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.True(_hasher.VerifyPassword(rawPassword, userId, hash));
        Assert.False(_hasher.VerifyPassword("WrongPassword123!", userId, hash));
    }

    [Fact]
    public void ComputeRowSignature_ShouldDetectDirectDatabaseTampering()
    {
        // 1. Arrange user row data
        const int userId = 25;
        const string email = "admin@pettycash.local";
        const string passwordHash = "$2a$12$K3xqJ8vH5nY0mR2pL4wQ.eO9sT1uV6xY7zA8bC0dE1fG2hI3jK4lM";

        // 2. Act: Compute row signature
        var signature = _hasher.ComputeRowSignature(userId, email, passwordHash);

        // 3. Assert: Signature must match original data and fail if altered
        Assert.True(_hasher.VerifyRowSignature(userId, email, passwordHash, signature));

        // Tamper with email address
        Assert.False(_hasher.VerifyRowSignature(userId, "tampered@pettycash.local", passwordHash, signature));

        // Tamper with password hash directly in the database
        Assert.False(_hasher.VerifyRowSignature(userId, email, "$2a$12$HACKED_HASH_HERE", signature));
    }
}
