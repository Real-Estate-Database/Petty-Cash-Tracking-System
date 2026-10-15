using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PettyCashDatabase.Shared.Auth;
using PettyCashDatabase.Shared.DTOs.Auth;
using PettyCashDatabase.Web.Authentication;
using PettyCashDatabase.Web.Data;
using PettyCashDatabase.Web.Data.Models;
using PettyCashDatabase.Web.Middlewares;
using PettyCashDatabase.Web.Options;

namespace PettyCashDatabase.Web.Services;

/// <summary>
/// Server authentication contract defining login, registration, token refresh, and password management.
/// </summary>
public interface IServerAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, ClientRequestInfo client, CancellationToken cancellationToken = default);
    Task RegisterTenantAsync(TenantOnboardingDto request, ClientRequestInfo client, CancellationToken cancellationToken = default);
    Task ForgotPasswordAsync(ForgotPasswordDto request, ClientRequestInfo client, CancellationToken cancellationToken = default);
    Task ResendResetEmailAsync(ResendResetEmailDto request, ClientRequestInfo client, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordDto request, ClientRequestInfo client, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, ClientRequestInfo client, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, ClientRequestInfo client, CancellationToken cancellationToken = default);
    Task UnlockUserAsync(int targetUserId, int actingAdminUserId, ClientRequestInfo client, CancellationToken cancellationToken = default);
}

/// <summary>
/// Client request metadata captured for security auditing and tamper-evidence.
/// </summary>
public sealed record ClientRequestInfo(
    string? IpAddress,
    string? UserAgent,
    string? ClientType,
    string? DeviceIdentifier);

/// <summary>
/// Enterprise authentication service implementing progressive delays, PBKDF2 hashing,
/// HMAC row signatures, and parameterized raw SQL queries for data access.
/// </summary>
public sealed class AuthService : IServerAuthService
{
    private const int MaxLoginAttempts = LoginSecurityPolicy.MaxFailedAttempts;
    private readonly AppDbContext _db;
    private readonly PasswordHasher _passwordHasher;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly CustomClaimsFactory _claimsFactory;
    private readonly IEmailService _emailService;
    private readonly IUserTimeZoneService _timeZoneService;
    private readonly AppOptions _appOptions;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db,
        PasswordHasher passwordHasher,
        JwtTokenGenerator tokenGenerator,
        CustomClaimsFactory claimsFactory,
        IEmailService emailService,
        IUserTimeZoneService timeZoneService,
        IOptions<AppOptions> appOptions,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _claimsFactory = claimsFactory;
        _emailService = emailService;
        _timeZoneService = timeZoneService;
        _appOptions = appOptions.Value;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user by email or telephone, enforces lockout policy, validates PBKDF2 hash
    /// and tamper-evident row signature, and issues JWT tokens.
    /// </summary>
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        // 1. Normalize identifier (email or telephone number)
        var identifier = request.Identifier.Trim();
        User? user;

        // 2. Fetch User and Organization record via raw SQL SELECT
        if (ContactNormalizer.LooksLikeEmail(identifier))
        {
            var email = ContactNormalizer.NormalizeEmail(identifier);
            user = await _db.Users
                .FromSqlInterpolated($@"
                    SELECT * FROM [Users] 
                    WHERE [Email] = {email}
                ")
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            var phone = ContactNormalizer.NormalizeTelephone(identifier);
            user = await _db.Users
                .FromSqlInterpolated($@"
                    SELECT * FROM [Users] 
                    WHERE [TelephoneNumber] = {phone}
                ")
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // 3. Constant-time dummy verify and progressive delay if user does not exist
        if (user is null)
        {
            _passwordHasher.DummyVerify(request.Password);
            await Task.Delay(LoginSecurityPolicy.GetProgressiveDelay(1), cancellationToken);
            throw new UnauthorizedAccessException("Invalid email/telephone or password.");
        }

        // 4. Validate temporary lockout window
        var nowUtc = DateTime.UtcNow;
        if (user.LockoutUntil is { } lockoutUntil && lockoutUntil > nowUtc)
        {
            throw new AccountLockedException();
        }

        // 5. Automatically clear expired lockout window via raw SQL UPDATE
        if (user.LockoutUntil is not null && user.LockoutUntil <= nowUtc)
        {
            user.FailedLoginCount = 0;
            user.LockoutUntil = null;
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE [Users]
                SET [FailedLoginCount] = 0, [LockoutUntil] = NULL
                WHERE [UserId] = {user.UserId};
            ", cancellationToken);
        }

        var userNow = _timeZoneService.GetCurrentTime(user.TimeZoneId, user.TimeZone ?? 3);

        if (user.IsLocked)
            throw new AccountLockedException("Account locked. Contact your organization administrator.");

        // 6. Verify cryptographic tamper-evident row signature
        var rowSignatureValid = _passwordHasher.VerifyRowSignature(user.UserId, user.Email, user.PasswordHash, user.RowSignature);
        if (!rowSignatureValid)
        {
            // Lock tampered account via raw SQL UPDATE
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE [Users]
                SET [IsLocked] = 1, [LockedDate] = {userNow}
                WHERE [UserId] = {user.UserId};
            ", cancellationToken);

            await WriteEventAsync(user.UserId, AuditEvents.TamperDetected, client, cancellationToken);
            await RevokeAllLiveSessionsAsync(user.UserId, cancellationToken);
            throw new TamperDetectedException();
        }

        // 7. Verify password hash using PBKDF2
        var passwordValid = _passwordHasher.VerifyPassword(request.Password, user.UserId, user.PasswordHash);
        if (!passwordValid)
        {
            _logger.LogWarning(
                "Login password verification failed for user {UserId} and identifier {Identifier}. Submitted password length: {PasswordLength}.",
                user.UserId,
                ContactNormalizer.LooksLikeEmail(identifier) ? ContactNormalizer.NormalizeEmail(identifier) : "telephone",
                request.Password.Length);

            var failedAttempt = ++user.FailedLoginCount;
            if (failedAttempt >= MaxLoginAttempts)
            {
                var newLockoutUntil = nowUtc.Add(LoginSecurityPolicy.LockoutDuration);
                // Update user lockout status via raw SQL UPDATE
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE [Users]
                    SET [FailedLoginCount] = {failedAttempt}, 
                        [LockoutUntil] = {newLockoutUntil}, 
                        [LockedDate] = {userNow}
                    WHERE [UserId] = {user.UserId};
                ", cancellationToken);

                await WriteEventAsync(user.UserId, AuditEvents.Locked, client, cancellationToken);
                await RevokeAllLiveSessionsAsync(user.UserId, cancellationToken);
            }
            else
            {
                // Increment failed login count via raw SQL UPDATE
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE [Users]
                    SET [FailedLoginCount] = {failedAttempt}
                    WHERE [UserId] = {user.UserId};
                ", cancellationToken);

                await WriteEventAsync(user.UserId, AuditEvents.LoginFailed, client, cancellationToken);
            }

            await Task.Delay(LoginSecurityPolicy.GetProgressiveDelay(failedAttempt), cancellationToken);

            if (user.LockoutUntil is not null)
                throw new AccountLockedException();

            throw new UnauthorizedAccessException("Invalid email/telephone or password.");
        }

        // 8. Verify tenant organization active and not deleted/disabled
        if (user.Disabled || user.Deleted || !user.IsActive
            || user.Organization.Disabled || user.Organization.Deleted)
            throw new UnauthorizedAccessException("Invalid email/telephone or password.");

        // 9. Reset failed login attempts and record last login timestamp via raw SQL UPDATE
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE [Users]
            SET [FailedLoginCount] = 0, 
                [LockoutUntil] = NULL, 
                [LockedDate] = NULL, 
                [LastLoginDate] = {userNow}
            WHERE [UserId] = {user.UserId};
        ", cancellationToken);

        // 10. Issue JWT token pair and record audit session
        return await IssueTokensAsync(user, AuditEvents.Login, client, cancellationToken);
    }

    /// <summary>
    /// Onboards a new organization tenant, registers the primary administrator,
    /// assigns default administrative permissions, and records the initial audit log.
    /// </summary>
    public async Task RegisterTenantAsync(TenantOnboardingDto request, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        var orgEmail = ContactNormalizer.NormalizeEmail(request.OrganizationEmail);
        var adminEmail = ContactNormalizer.NormalizeEmail(request.Email);

        // 1. Validate organization country and dial code via raw SQL SELECT
        var organizationCountry = await _db.Countries
            .FromSqlInterpolated($@"
                SELECT * FROM [Countries]
                WHERE [ID] = {request.OrganizationCountryId}
            ")
            .FirstOrDefaultAsync(cancellationToken);

        if (organizationCountry == null)
            throw new InvalidOperationException("The selected country could not be found.");

        var countryDialCode = CountryPhoneCatalog.Find(
            organizationCountry.Code,
            organizationCountry.CountryName)?.DialCode;
        if (string.IsNullOrWhiteSpace(countryDialCode))
            throw new InvalidOperationException("A telephone country code is not configured for the selected country.");

        var orgPhone = ContactNormalizer.NormalizeTelephone(request.OrganizationTelephone, countryDialCode);
        var adminPhone = ContactNormalizer.NormalizeTelephone(request.TelephoneNumber, countryDialCode);

        // 2. Validate password policy and confirmation
        if (!PasswordPolicy.IsValid(request.Password, adminEmail))
            throw new InvalidOperationException("Password does not meet policy requirements.");

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            throw new InvalidOperationException("Passwords do not match.");

        var selectedTimeZone = TimeZoneCatalog.Resolve(request.TimeZoneId, request.TimeZone ?? 3);

        // 3. Validate geographical hierarchy (Country -> Region -> District)
        await ValidateRegistrationLocationsAsync(request, cancellationToken);

        // 4. Begin transactional boundary
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // 5. Prevent duplicate registrations via raw SQL SELECT checks
        var emailTakenCount = await _db.Database.SqlQuery<int>($@"
            SELECT COUNT(1) AS Value FROM [Users] WHERE [Email] = {adminEmail}
            UNION ALL
            SELECT COUNT(1) AS Value FROM [Organizations] WHERE [Email] = {orgEmail};
        ").ToListAsync(cancellationToken);

        if (emailTakenCount.Any(c => c > 0))
            throw new InvalidOperationException("The organization or administrator email is already registered.");

        var phoneTakenCount = await _db.Database.SqlQuery<int>($@"
            SELECT COUNT(1) AS Value FROM [Users] WHERE [TelephoneNumber] = {adminPhone}
            UNION ALL
            SELECT COUNT(1) AS Value FROM [Organizations] WHERE [Telephone] = {orgPhone};
        ").ToListAsync(cancellationToken);

        if (phoneTakenCount.Any(c => c > 0))
            throw new InvalidOperationException("The organization or administrator telephone is already registered.");

        var orgNow = _timeZoneService.GetCurrentTime(selectedTimeZone.Id, request.TimeZone ?? 3);
        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        var timeZoneOffset = TimeZoneCatalog.GetOffsetHours(selectedTimeZone.Id, request.TimeZone ?? 3);

        // 6. Create tenant Organization record via raw SQL INSERT
        var organizationId = await _db.Database.SqlQuery<int>($@"
            INSERT INTO [Organizations] 
            ([OrganizationName], [Address], [CountryId], [RegionId], [DistrictId], [Email], [Telephone], [Disabled], [Deleted], [CreatedAt], [UpdatedAt])
            OUTPUT INSERTED.OrganizationId
            VALUES 
            ({request.OrganizationName.Trim()}, {address}, {request.OrganizationCountryId}, {request.OrganizationRegionId}, {request.OrganizationDistrictId}, {orgEmail}, {orgPhone}, 0, 0, {orgNow}, {orgNow});
        ").SingleAsync(cancellationToken);

        // 7. Create primary Administrator User account via raw SQL INSERT
        var userId = await _db.Database.SqlQuery<int>($@"
            INSERT INTO [Users]
            ([OrganizationId], [FirstName], [LastName], [Email], [TelephoneNumber], [PasswordHash], [IsActive], [Disabled], [Deleted], [CountryId], [RegionId], [DistrictId], [TimeZone], [TimeZoneId], [EntryDate], [IsOrgAdmin], [FailedLoginCount], [IsLocked])
            OUTPUT INSERTED.UserId
            VALUES
            ({organizationId}, {request.FirstName.Trim()}, {request.LastName.Trim()}, {adminEmail}, {adminPhone}, '', 1, 0, 0, {request.OrganizationCountryId}, {request.OrganizationRegionId}, {request.OrganizationDistrictId}, {timeZoneOffset}, {selectedTimeZone.Id}, {orgNow}, 1, 0, 0);
        ").SingleAsync(cancellationToken);

        // 8. Hash password with PBKDF2 & compute cryptographic tamper-evident signature via raw SQL UPDATE
        var passwordHash = _passwordHasher.HashPassword(request.Password, userId);
        var rowSignature = _passwordHasher.ComputeRowSignature(userId, adminEmail, passwordHash);

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE [Users]
            SET [PasswordHash] = {passwordHash}, [RowSignature] = {rowSignature}
            WHERE [UserId] = {userId};
        ", cancellationToken);

        // 9. Grant default permissions and write immutable audit trail
        await PermissionSyncService.GrantAllPermissionsAsync(_db, userId, cancellationToken);
        await WriteEventAsync(userId, AuditEvents.TenantRegistered, client, cancellationToken);

        // 10. Commit transaction
        await tx.CommitAsync(cancellationToken);
    }

    private async Task ValidateRegistrationLocationsAsync(
        TenantOnboardingDto request,
        CancellationToken cancellationToken)
    {
        // 1. Validate country via raw SQL SELECT
        var organizationCountry = await _db.Countries
            .FromSqlInterpolated($@"
                SELECT * FROM [Countries]
                WHERE [ID] = {request.OrganizationCountryId}
            ")
            .FirstOrDefaultAsync(cancellationToken);

        if (organizationCountry is null)
            throw new InvalidOperationException("The selected organization country is not available.");

        // 2. Validate region matches country via raw SQL SELECT
        if (request.OrganizationRegionId is int organizationRegionId)
        {
            var regionMatches = await _db.Database.SqlQuery<int>($@"
                SELECT COUNT(1) AS Value FROM [Regions]
                WHERE [RegionID] = {organizationRegionId} AND [Country] = {organizationCountry.Id};
            ").SingleAsync(cancellationToken);

            if (regionMatches == 0)
                throw new InvalidOperationException("The selected organization region is not valid for the country.");
        }

        var isUganda = organizationCountry.CountryName?.Equals("Uganda", StringComparison.OrdinalIgnoreCase) == true
            || organizationCountry.Code?.Equals("UGA", StringComparison.OrdinalIgnoreCase) == true;

        // 3. Validate Uganda district rules via raw SQL SELECT
        if (isUganda)
        {
            if (request.OrganizationDistrictId is not int districtId || districtId <= 0)
                throw new InvalidOperationException("District is required for organizations in Uganda.");

            var districtMatches = await _db.Database.SqlQuery<int>($@"
                SELECT COUNT(1) AS Value FROM [Districts]
                WHERE [DistrictID] = {districtId} 
                  AND [Country] = {organizationCountry.Id}
                  AND ({request.OrganizationRegionId} IS NULL OR [Region] = {request.OrganizationRegionId});
            ").SingleAsync(cancellationToken);

            if (districtMatches == 0)
                throw new InvalidOperationException("The selected organization district is not valid for the location.");
        }
        else
        {
            request.OrganizationRegionId = null;
            request.OrganizationDistrictId = null;
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordDto request, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        await RequestPasswordResetCoreAsync(request.Email, enforceHourlyLimit: false, client, cancellationToken);
    }

    public async Task ResendResetEmailAsync(ResendResetEmailDto request, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        await RequestPasswordResetCoreAsync(request.Email, enforceHourlyLimit: true, client, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto request, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        // 1. Validate password policy
        var email = ContactNormalizer.NormalizeEmail(request.Email);
        if (!PasswordPolicy.IsValid(request.NewPassword, email))
        {
            _logger.LogWarning("Password reset rejected because the password policy failed for {Email}.", email);
            throw new InvalidOperationException("Password does not meet policy requirements.");
        }

        var tokenHash = PasswordHasher.Sha256Hex(request.Token);

        // 2. Query password reset token via raw SQL SELECT
        var reset = await _db.PasswordResetTokens
            .FromSqlInterpolated($@"
                SELECT TOP 1 * FROM [PasswordResetTokens]
                WHERE [TokenHash] = {tokenHash}
                ORDER BY [EntryDate] DESC
            ")
            .Include(t => t.User)
            .FirstOrDefaultAsync(cancellationToken);

        if (reset is null)
        {
            _logger.LogWarning("Password reset rejected because no token matched hash prefix {TokenHashPrefix} for {Email}.", tokenHash[..8], email);
            throw new InvalidOperationException("Invalid or expired reset token.");
        }

        if (reset.UsedDate is not null)
        {
            _logger.LogWarning("Password reset rejected because token {PasswordResetTokenId} was already used at {UsedDate}.", reset.PasswordResetTokenId, reset.UsedDate);
            throw new InvalidOperationException("Invalid or expired reset token.");
        }

        if (!string.Equals(reset.User.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Password reset rejected because the token email did not match the request email.");
            throw new InvalidOperationException("Invalid or expired reset token.");
        }

        var user = reset.User;
        var localNow = _timeZoneService.GetCurrentTime(user.TimeZoneId, user.TimeZone ?? 3);
        if (reset.ExpiredDate <= localNow)
        {
            _logger.LogWarning("Password reset rejected because token {PasswordResetTokenId} expired at {ExpiredDate}; current user-local time is {LocalNow}.", reset.PasswordResetTokenId, reset.ExpiredDate, localNow);
            throw new InvalidOperationException("Invalid or expired reset token.");
        }

        // 3. Compute new password hash and row signature
        var newHash = _passwordHasher.HashPassword(request.NewPassword, user.UserId);
        var newSig = _passwordHasher.ComputeRowSignature(user.UserId, user.Email, newHash);

        // 4. Update user password and mark reset token used via raw SQL UPDATE
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE [Users]
            SET [PasswordHash] = {newHash}, [RowSignature] = {newSig}
            WHERE [UserId] = {user.UserId};
        ", cancellationToken);

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE [PasswordResetTokens]
            SET [UsedDate] = {localNow}
            WHERE [PasswordResetTokenId] = {reset.PasswordResetTokenId};
        ", cancellationToken);

        // 5. Revoke all active sessions and record audit event
        await RevokeAllLiveSessionsAsync(user.UserId, cancellationToken);
        await WriteEventAsync(user.UserId, AuditEvents.PasswordReset, client, cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        // 1. Locate session by refresh token hash via raw SQL SELECT
        var hash = PasswordHasher.Sha256Hex(refreshToken);

        var session = await _db.AuditTrails
            .FromSqlInterpolated($@"
                SELECT * FROM [AuditTrail]
                WHERE [RefreshTokenHash] = {hash};
            ")
            .Include(a => a.User).ThenInclude(u => u.Organization)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        var user = session.User;
        var now = _timeZoneService.GetCurrentTime(user.TimeZoneId, user.TimeZone ?? 3);

        // 2. Validate token expiry
        if (session.ExpiryDate < now)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // 3. Detect token reuse
        if (session.IsRevoked)
        {
            await RevokeAllLiveSessionsAsync(session.UserId, cancellationToken);
            await WriteEventAsync(session.UserId, AuditEvents.RefreshReuseDetected, client, cancellationToken);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        // 4. Check user lockout status
        if (user.IsLocked)
            throw new AccountLockedException();

        // 5. Verify row signature integrity
        if (!_passwordHasher.VerifyRowSignature(user.UserId, user.Email, user.PasswordHash, user.RowSignature))
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE [Users]
                SET [IsLocked] = 1, [LockedDate] = {now}
                WHERE [UserId] = {user.UserId};
            ", cancellationToken);

            await WriteEventAsync(user.UserId, AuditEvents.TamperDetected, client, cancellationToken);
            await RevokeAllLiveSessionsAsync(user.UserId, cancellationToken);
            throw new TamperDetectedException();
        }

        if (user.Disabled || user.Deleted || !user.IsActive
            || user.Organization.Disabled || user.Organization.Deleted)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // 6. Revoke current session token via raw SQL UPDATE
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE [AuditTrail]
            SET [IsRevoked] = 1, [RevokedDate] = {now}
            WHERE [AuditTrailId] = {session.AuditTrailId};
        ", cancellationToken);

        // 7. Issue new tokens and record audit session
        return await IssueTokensAsync(user, AuditEvents.TokenRefresh, client, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        // 1. Locate session via raw SQL SELECT
        var hash = PasswordHasher.Sha256Hex(refreshToken);
        var session = await _db.AuditTrails
            .FromSqlInterpolated($@"
                SELECT * FROM [AuditTrail]
                WHERE [RefreshTokenHash] = {hash} 
                  AND [IsRevoked] = 0 
                  AND [RefreshTokenHash] <> {AuditEvents.EventTokenMarker};
            ")
            .Include(a => a.User)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
            return;

        var now = _timeZoneService.GetCurrentTime(session.User?.TimeZoneId, session.User?.TimeZone ?? 3);

        // 2. Revoke session via raw SQL UPDATE
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE [AuditTrail]
            SET [IsRevoked] = 1, [RevokedDate] = {now}
            WHERE [AuditTrailId] = {session.AuditTrailId};
        ", cancellationToken);

        // 3. Record logout audit event
        await WriteEventAsync(session.UserId, AuditEvents.Logout, client, cancellationToken);
    }

    // Break-glass: if the sole org admin is locked, unlock via SQL:
    // UPDATE Users SET IsLocked=0, FailedLoginCount=0, LockedDate=NULL, LockoutUntil=NULL WHERE UserId=@id AND IsOrgAdmin=1;
    public async Task UnlockUserAsync(int targetUserId, int actingAdminUserId, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        // 1. Verify acting admin via raw SQL SELECT
        var admin = await _db.Users
            .FromSqlInterpolated($@"SELECT * FROM [Users] WHERE [UserId] = {actingAdminUserId}")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException();

        // 2. Verify target user via raw SQL SELECT
        var target = await _db.Users
            .FromSqlInterpolated($@"SELECT * FROM [Users] WHERE [UserId] = {targetUserId}")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        if (target.OrganizationId != admin.OrganizationId)
            throw new UnauthorizedAccessException();

        // 3. Unlock target user via raw SQL UPDATE
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE [Users] 
            SET [IsLocked] = 0, [FailedLoginCount] = 0, [LockedDate] = NULL, [LockoutUntil] = NULL 
            WHERE [UserId] = {targetUserId} AND [OrganizationId] = {admin.OrganizationId};
        ", cancellationToken);

        // 4. Record unlock audit event
        await WriteEventAsync(
            target.UserId,
            $"{AuditEvents.Unlocked} | by:{actingAdminUserId}",
            client,
            cancellationToken);
    }

    private async Task RequestPasswordResetCoreAsync(
        string rawEmail,
        bool enforceHourlyLimit,
        ClientRequestInfo client,
        CancellationToken cancellationToken)
    {
        // 1. Normalize email and find user via raw SQL SELECT
        var email = ContactNormalizer.NormalizeEmail(rawEmail);
        var user = await _db.Users
            .FromSqlInterpolated($@"SELECT * FROM [Users] WHERE [Email] = {email}")
            .FirstOrDefaultAsync(cancellationToken);

        // Always succeed from caller perspective to prevent account enumeration
        if (user is null)
            return;

        var localNow = _timeZoneService.GetCurrentTime(user.TimeZoneId, user.TimeZone ?? 3);
        var since = localNow.AddHours(-1);

        // 2. Enforce rate limit via raw SQL SELECT COUNT
        if (enforceHourlyLimit)
        {
            var recentCount = await _db.Database.SqlQuery<int>($@"
                SELECT COUNT(1) AS Value FROM [PasswordResetTokens]
                WHERE [UserId] = {user.UserId} AND [EntryDate] >= {since};
            ").SingleAsync(cancellationToken);

            if (recentCount >= 3)
                throw new InvalidOperationException("Too many reset requests. Try again later.");
        }

        // 3. Invalidate previous unused reset tokens via raw SQL UPDATE
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE [PasswordResetTokens]
            SET [UsedDate] = {localNow}
            WHERE [UserId] = {user.UserId} AND [UsedDate] IS NULL;
        ", cancellationToken);

        // 4. Generate cryptographically secure reset token
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var hash = PasswordHasher.Sha256Hex(raw);
        var expiredDate = localNow.AddMinutes(30);

        // 5. Insert new password reset token via raw SQL INSERT
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO [PasswordResetTokens]
            ([UserId], [TokenHash], [ExpiredDate], [EntryDate])
            VALUES
            ({user.UserId}, {hash}, {expiredDate}, {localNow});
        ", cancellationToken);

        // 6. Record audit event and dispatch email
        await WriteEventAsync(user.UserId, AuditEvents.PasswordResetRequested, client, cancellationToken);

        var url = $"{_appOptions.BaseUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(raw)}";
        await _emailService.SendPasswordResetAsync(email, url, cancellationToken);
    }

    private async Task<AuthResponseDto> IssueTokensAsync(
        User user,
        string description,
        ClientRequestInfo client,
        CancellationToken cancellationToken)
    {
        // 1. Create claims and generate access/refresh tokens
        var claims = await _claimsFactory.CreateClaimsAsync(user, cancellationToken);
        var (accessToken, _, accessExpires) = _tokenGenerator.CreateAccessToken(claims);
        var (refreshToken, refreshExpires) = _tokenGenerator.CreateRefreshToken();

        var userNow = _timeZoneService.GetCurrentTime(user.TimeZoneId, user.TimeZone ?? 3);
        var accessTokenHash = PasswordHasher.Sha256Hex(accessToken);
        var refreshTokenHash = PasswordHasher.Sha256Hex(refreshToken);
        var clientType = Truncate(client.ClientType, 100);
        var deviceIdentifier = Truncate(client.DeviceIdentifier, 255);
        var ipAddress = Truncate(client.IpAddress, 50);
        var userAgent = Truncate(client.UserAgent, 500);
        var expiryDate = userNow.AddHours(_jwtOptions.RefreshTokenHours);
        var desc = Truncate(description, 255)!;

        // 2. Persist audit trail session via raw SQL INSERT
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO [AuditTrail]
            ([UserId], [AccessTokenHash], [RefreshTokenHash], [ClientType], [DeviceIdentifier], [IpAddress], [UserAgent], [CountryId], [EntryDate], [TimeZoneId], [ExpiryDate], [IsRevoked], [Description])
            VALUES
            ({user.UserId}, {accessTokenHash}, {refreshTokenHash}, {clientType}, {deviceIdentifier}, {ipAddress}, {userAgent}, NULL, {userNow}, {user.TimeZoneId}, {expiryDate}, 0, {desc});
        ", cancellationToken);

        // 3. Fetch user permissions
        var permissions = await _claimsFactory.GetPermissionCodesAsync(user.UserId, cancellationToken);

        // 4. Return authentication response payload
        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = accessExpires,
            UserProfile = new UserProfileDto
            {
                UserId = user.UserId,
                OrganizationId = user.OrganizationId,
                CountryId = user.CountryId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                TimeZoneId = user.TimeZoneId,
                Permissions = permissions
            }
        };
    }

    private async Task WriteEventAsync(int userId, string description, ClientRequestInfo client, CancellationToken cancellationToken)
    {
        // 1. Fetch user timezone via raw SQL SELECT
        var user = await _db.Users
            .FromSqlInterpolated($@"SELECT * FROM [Users] WHERE [UserId] = {userId}")
            .Select(u => new { u.TimeZoneId, u.TimeZone })
            .FirstOrDefaultAsync(cancellationToken);

        var now = _timeZoneService.GetCurrentTime(user?.TimeZoneId, user?.TimeZone ?? 3);
        var clientType = Truncate(client.ClientType, 100);
        var deviceIdentifier = Truncate(client.DeviceIdentifier, 255);
        var ipAddress = Truncate(client.IpAddress, 50);
        var userAgent = Truncate(client.UserAgent, 500);
        var desc = Truncate(description, 255)!;
        var tzId = user?.TimeZoneId;

        // 2. Record immutable audit event via raw SQL INSERT
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO [AuditTrail]
            ([UserId], [AccessTokenHash], [RefreshTokenHash], [ClientType], [DeviceIdentifier], [IpAddress], [UserAgent], [CountryId], [EntryDate], [TimeZoneId], [ExpiryDate], [IsRevoked], [RevokedDate], [Description])
            VALUES
            ({userId}, {AuditEvents.EventTokenMarker}, {AuditEvents.EventTokenMarker}, {clientType}, {deviceIdentifier}, {ipAddress}, {userAgent}, NULL, {now}, {tzId}, {now}, 1, {now}, {desc});
        ", cancellationToken);
    }

    private async Task RevokeAllLiveSessionsAsync(int userId, CancellationToken cancellationToken)
    {
        // 1. Fetch user timezone
        var user = await _db.Users
            .FromSqlInterpolated($@"SELECT * FROM [Users] WHERE [UserId] = {userId}")
            .Select(u => new { u.TimeZoneId, u.TimeZone })
            .FirstOrDefaultAsync(cancellationToken);

        var now = _timeZoneService.GetCurrentTime(user?.TimeZoneId, user?.TimeZone ?? 3);

        // 2. Invalidate all active sessions via raw SQL UPDATE
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE [AuditTrail]
            SET [IsRevoked] = 1, [RevokedDate] = {now}
            WHERE [UserId] = {userId} 
              AND [IsRevoked] = 0 
              AND [ExpiryDate] > {now} 
              AND [RefreshTokenHash] <> {AuditEvents.EventTokenMarker};
        ", cancellationToken);
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
