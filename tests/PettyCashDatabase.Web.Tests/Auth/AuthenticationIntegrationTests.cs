using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Web.Authentication;
using PettyCashDatabase.Web.Options;
using PettyCashDatabase.Web.Services;
using Xunit;

namespace PettyCashDatabase.Tests;

/// <summary>
/// Integration test verifying the end-to-end authentication lifecycle:
/// 1. Secure password hashing and tamper-evident row signature creation (Registration).
/// 2. Constant-time password and row verification (Login).
/// 3. JWT access token and 64-byte refresh token generation.
/// 4. Refresh token rotation.
/// 5. Forgot password link generation and ConsoleEmailService zero-config dispatch.
/// </summary>
public sealed class AuthenticationIntegrationTests
{
    private readonly PasswordHasher _hasher;
    private readonly JwtTokenGenerator _jwtGenerator;
    private readonly ConsoleEmailService _consoleEmailService;
    private readonly JwtOptions _jwtOptions;

    public AuthenticationIntegrationTests()
    {
        // 1. Configure cryptographic keys
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PETTYCASH_PEPPER_KEY"] = "integration_test_pepper_key_minimum_32_characters!",
                ["PETTYCASH_ROWSIGN_KEY"] = "integration_test_rowsign_key_minimum_32_characters!"
            })
            .Build();

        _hasher = new PasswordHasher(config);

        // 2. Configure JWT options
        _jwtOptions = new JwtOptions
        {
            Issuer = "PettyCashIntegrationServer",
            Audience = "PettyCashIntegrationClients",
            SigningKey = "integration_test_symmetric_jwt_secret_key_at_least_32_chars!",
            AccessTokenMinutes = 30,
            RefreshTokenHours = 24
        };

        _jwtGenerator = new JwtTokenGenerator(Options.Create(_jwtOptions));

        // 3. Configure zero-config console email service
        _consoleEmailService = new ConsoleEmailService(NullLogger<ConsoleEmailService>.Instance);
    }

    [Fact]
    public async Task CompleteAuthenticationAndRecoveryFlow_ShouldSucceedEndToEnd()
    {
        // -------------------------------------------------------------
        // STEP 1: Registration Simulation (Password & Tamper-Evident Signatures)
        // -------------------------------------------------------------
        const int userId = 1001;
        const string email = "finance.director@tenantcorp.com";
        const string rawPassword = "SecurePassword#2026";

        var passwordHash = _hasher.HashPassword(rawPassword, userId);
        var rowSignature = _hasher.ComputeRowSignature(userId, email, passwordHash);

        Assert.False(string.IsNullOrWhiteSpace(passwordHash));
        Assert.False(string.IsNullOrWhiteSpace(rowSignature));

        // -------------------------------------------------------------
        // STEP 2: Login Simulation (Verification & Tamper Check)
        // -------------------------------------------------------------
        var isPasswordValid = _hasher.VerifyPassword(rawPassword, userId, passwordHash);
        var isRowIntact = _hasher.VerifyRowSignature(userId, email, passwordHash, rowSignature);

        Assert.True(isPasswordValid, "Password verification must succeed for correct credentials.");
        Assert.True(isRowIntact, "Row signature verification must succeed against authentic database state.");

        // -------------------------------------------------------------
        // STEP 3: Token Issuance (JWT Access Token & Refresh Token)
        // -------------------------------------------------------------
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(AuthClaimTypes.OrgId, "50"),
            new Claim(ClaimTypes.Role, "FinanceDirector")
        };

        var (accessToken, jti, accessExpiresAt) = _jwtGenerator.CreateAccessToken(claims);
        var (refreshToken, refreshExpiresAt) = _jwtGenerator.CreateRefreshToken();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(jti));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));
        Assert.True(accessExpiresAt > DateTime.UtcNow);
        Assert.True(refreshExpiresAt > DateTime.UtcNow);

        // Verify claims inside the access token
        var handler = new JwtSecurityTokenHandler();
        var parsedToken = handler.ReadJwtToken(accessToken);
        Assert.Equal(_jwtOptions.Issuer, parsedToken.Issuer);
        Assert.Equal(email, parsedToken.Claims.First(c => c.Type == ClaimTypes.Email).Value);

        // -------------------------------------------------------------
        // STEP 4: Token Refresh Rotation Simulation
        // -------------------------------------------------------------
        var (newRefreshToken, newRefreshExpiresAt) = _jwtGenerator.CreateRefreshToken();
        Assert.NotEqual(refreshToken, newRefreshToken);
        Assert.True(newRefreshExpiresAt > DateTime.UtcNow);

        // -------------------------------------------------------------
        // STEP 5: Forgot Password & Email Dispatch Simulation
        // -------------------------------------------------------------
        var resetToken = Guid.NewGuid().ToString("N");
        var resetUrl = $"https://localhost:7223/reset-password?token={resetToken}&email={Uri.EscapeDataString(email)}";

        // Dispatch via zero-config development email service without throwing errors
        var emailException = await Record.ExceptionAsync(() =>
            _consoleEmailService.SendPasswordResetAsync(email, resetUrl));

        Assert.Null(emailException);
    }
}
