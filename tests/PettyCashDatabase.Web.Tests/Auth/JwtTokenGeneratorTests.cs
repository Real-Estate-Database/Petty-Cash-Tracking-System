using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Web.Authentication;
using PettyCashDatabase.Web.Options;
using Xunit;

namespace PettyCashDatabase.Tests;

/// <summary>
/// Unit tests verifying JWT access token generation, claims packaging, and cryptographic refresh token generation.
/// </summary>
public sealed class JwtTokenGeneratorTests
{
    private readonly JwtOptions _options = new()
    {
        Issuer = "PettyCashTestIssuer",
        Audience = "PettyCashTestAudience",
        SigningKey = "super_secret_test_key_with_sufficient_entropy_1234567890!",
        AccessTokenMinutes = 30,
        RefreshTokenHours = 24
    };

    [Fact]
    public void CreateAccessToken_ShouldEmitValidJwt_WithExpectedClaimsAndJti()
    {
        // 1. Arrange token generator with test options
        var generator = new JwtTokenGenerator(Options.Create(_options));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Email, "accountant@pettycash.local"),
            new Claim(AuthClaimTypes.OrgId, "101"),
            new Claim(ClaimTypes.Role, "Accountant")
        };

        // 2. Act: Generate access token
        var (accessToken, jti, expiresAt) = generator.CreateAccessToken(claims);

        // 3. Assert: Validate token structure and claims
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(jti));
        Assert.True(expiresAt > DateTime.UtcNow);

        // 4. Decode and verify internal claims
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(accessToken);

        Assert.Equal(_options.Issuer, token.Issuer);
        Assert.Equal(_options.Audience, token.Audiences.First());
        Assert.Equal("42", token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("accountant@pettycash.local", token.Claims.First(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("101", token.Claims.First(c => c.Type == AuthClaimTypes.OrgId).Value);
        Assert.Equal(jti, token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }

    [Fact]
    public void CreateRefreshToken_ShouldReturnCryptographicUrlSafeToken()
    {
        // 1. Arrange token generator
        var generator = new JwtTokenGenerator(Options.Create(_options));

        // 2. Act: Generate two consecutive refresh tokens
        var (token1, expires1) = generator.CreateRefreshToken();
        var (token2, expires2) = generator.CreateRefreshToken();

        // 3. Assert: Ensure high-entropy randomness and uniqueness
        Assert.False(string.IsNullOrWhiteSpace(token1));
        Assert.False(string.IsNullOrWhiteSpace(token2));
        Assert.NotEqual(token1, token2);
        Assert.True(token1.Length >= 64);
        Assert.True(expires1 > DateTime.UtcNow);
        Assert.True(expires2 > DateTime.UtcNow);
    }
}
