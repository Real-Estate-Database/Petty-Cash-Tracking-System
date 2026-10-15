using PettyCashDatabase.Shared.DTOs.Auth;

namespace PettyCashDatabase.Shared.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task RegisterTenantAsync(TenantOnboardingDto request, CancellationToken cancellationToken = default);

    Task ForgotPasswordAsync(ForgotPasswordDto request, CancellationToken cancellationToken = default);

    Task ResendResetEmailAsync(ResendResetEmailDto request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordDto request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}
