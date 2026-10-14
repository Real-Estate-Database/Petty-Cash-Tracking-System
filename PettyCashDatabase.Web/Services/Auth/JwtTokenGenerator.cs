using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PettyCashDatabase.Web.Options;

namespace PettyCashDatabase.Web.Authentication;

/// <summary>
/// Generates cryptographically signed JWT access tokens and cryptographically random refresh tokens.
/// </summary>
public sealed class JwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options) => _options = options.Value;

    /// <summary>
    /// Creates a signed JWT access token containing identity claims and unique JTI.
    /// </summary>
    public (string AccessToken, string Jti, DateTime ExpiresAt) CreateAccessToken(IEnumerable<Claim> claims)
    {
        // 1. Generate unique token identifier (JTI) and expiration timestamp
        var jti = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        // 2. Prepare claims list and ensure unique JTI
        var list = claims.ToList();
        list.RemoveAll(c => c.Type == JwtRegisteredClaimNames.Jti);
        list.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));

        // 3. Prepare symmetric signing key credentials
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 4. Construct JWT descriptor
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: list,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);

        // 5. Serialize token to string representation
        return (new JwtSecurityTokenHandler().WriteToken(token), jti, expires);
    }

    /// <summary>
    /// Creates a 64-byte cryptographically secure random refresh token encoded in URL-safe base64.
    /// </summary>
    public (string RefreshToken, DateTime ExpiresAt) CreateRefreshToken()
    {
        // 1. Generate 64 cryptographically secure random bytes
        var bytes = RandomNumberGenerator.GetBytes(64);

        // 2. Encode to URL-safe Base64 string
        var token = Base64UrlEncoder.Encode(bytes);

        // 3. Calculate expiration timestamp
        var expires = DateTime.UtcNow.AddHours(_options.RefreshTokenHours);

        return (token, expires);
    }
}
