using System.ComponentModel.DataAnnotations;

namespace PettyCashDatabase.Web.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(64)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 30;

    [Range(1, 168)]
    public int RefreshTokenHours { get; set; } = 24;

    public int ClockSkewSeconds { get; set; }
}

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public string User { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    [Required]
    public string From { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = true;
}

public sealed class AppOptions
{
    public const string SectionName = "App";

    [Required]
    public string BaseUrl { get; set; } = string.Empty;
}
