namespace PettyCashDatabase.Shared.DTOs.Auth;

public sealed class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public UserProfileDto UserProfile { get; set; } = new();
}

public sealed class UserProfileDto
{
    public int UserId { get; set; }

    public int OrganizationId { get; set; }

    public int? CountryId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? TimeZoneId { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public IReadOnlyList<string> Permissions { get; set; } = [];
}

public sealed class RefreshTokenRequestDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class LogoutRequestDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string RefreshToken { get; set; } = string.Empty;
}
