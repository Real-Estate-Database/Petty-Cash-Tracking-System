using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PettyCashDatabase.Web.Options;

namespace PettyCashDatabase.Web.Authentication;

public sealed class JwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options) => _options = options.Value;

    public (string AccessToken, string Jti, DateTime ExpiresAt) CreateAccessToken(IEnumerable<Claim> claims)
    {
        var jti = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var list = claims.ToList();
        list.RemoveAll(c => c.Type == JwtRegisteredClaimNames.Jti);
        list.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: list,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), jti, expires);
    }

    public (string RefreshToken, DateTime ExpiresAt) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Base64UrlEncoder.Encode(bytes);
        var expires = DateTime.UtcNow.AddHours(_options.RefreshTokenHours);
        return (token, expires);
    }
}
