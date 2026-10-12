namespace PettyCashDatabase.Shared.Interfaces;

public interface ITokenStorage
{
    Task<(string? AccessToken, string? RefreshToken, DateTime? ExpiresAt)> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(string accessToken, string refreshToken, DateTime expiresAt, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
