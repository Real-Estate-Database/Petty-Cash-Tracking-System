using System.Security.Cryptography;
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

public sealed record ClientRequestInfo(
    string? IpAddress,
    string? UserAgent,
    string? ClientType,
    string? DeviceIdentifier);

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

    public AuthService(
        AppDbContext db,
        PasswordHasher passwordHasher,
        JwtTokenGenerator tokenGenerator,
        CustomClaimsFactory claimsFactory,
        IEmailService emailService,
        IUserTimeZoneService timeZoneService,
        IOptions<AppOptions> appOptions,
        IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _claimsFactory = claimsFactory;
        _emailService = emailService;
        _timeZoneService = timeZoneService;
        _appOptions = appOptions.Value;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        var identifier = request.Identifier.Trim();
        User? user;

        if (ContactNormalizer.LooksLikeEmail(identifier))
        {
            var email = ContactNormalizer.NormalizeEmail(identifier);
            user = await _db.Users.IgnoreQueryFilters()
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }
        else
        {
            var phone = ContactNormalizer.NormalizeTelephone(identifier);
            user = await _db.Users.IgnoreQueryFilters()
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.TelephoneNumber == phone, cancellationToken);
        }

        if (user is null)
        {
            _passwordHasher.DummyVerify(request.Password);
            await Task.Delay(LoginSecurityPolicy.GetProgressiveDelay(1), cancellationToken);
            throw new UnauthorizedAccessException("Invalid email/telephone or password.");
        }

        var nowUtc = DateTime.UtcNow;
        if (user.LockoutUntil is { } lockoutUntil && lockoutUntil > nowUtc)
        {
            // Do not verify the password while the account is locked. This is
            // deliberately a fast 423 response and does not reveal account data.
            throw new AccountLockedException();
        }

        if (user.LockoutUntil is not null && user.LockoutUntil <= nowUtc)
        {
            user.FailedLoginCount = 0;
            user.LockoutUntil = null;
        }

        var userNow = _timeZoneService.GetCurrentTime(user.TimeZoneId, user.TimeZone ?? 3);

        if (user.IsLocked)
            throw new AccountLockedException("Account locked. Contact your organization administrator.");

        if (!_passwordHasher.VerifyRowSignature(user.UserId, user.Email, user.PasswordHash, user.RowSignature))
        {
            user.IsLocked = true;
            user.LockedDate = userNow;
            await WriteEventAsync(user.UserId, AuditEvents.TamperDetected, client, cancellationToken);
            await RevokeAllLiveSessionsAsync(user.UserId, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            throw new TamperDetectedException();
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.UserId, user.PasswordHash))
        {
            var failedAttempt = ++user.FailedLoginCount;
            if (failedAttempt >= MaxLoginAttempts)
            {
                user.LockoutUntil = nowUtc.Add(LoginSecurityPolicy.LockoutDuration);
                user.LockedDate = userNow;
                await WriteEventAsync(user.UserId, AuditEvents.Locked, client, cancellationToken);
                await RevokeAllLiveSessionsAsync(user.UserId, cancellationToken);
            }
            else
            {
                await WriteEventAsync(user.UserId, AuditEvents.LoginFailed, client, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await Task.Delay(LoginSecurityPolicy.GetProgressiveDelay(failedAttempt), cancellationToken);

            if (user.LockoutUntil is not null)
                throw new AccountLockedException();

            throw new UnauthorizedAccessException("Invalid email/telephone or password.");
        }

        if (user.Disabled || user.Deleted || !user.IsActive
            || user.Organization.Disabled || user.Organization.Deleted)
            throw new UnauthorizedAccessException("Invalid email/telephone or password.");

        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        user.LockedDate = null;
        user.LastLoginDate = userNow;
        return await IssueTokensAsync(user, AuditEvents.Login, client, cancellationToken);
    }

    public async Task RegisterTenantAsync(TenantOnboardingDto request, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        var orgEmail = ContactNormalizer.NormalizeEmail(request.OrganizationEmail);
        var adminEmail = ContactNormalizer.NormalizeEmail(request.Email);

        var organizationCountry = await _db.Countries.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.OrganizationCountryId, cancellationToken);
        if (organizationCountry == null)
            throw new InvalidOperationException("The selected country could not be found.");

        var countryDialCode = CountryPhoneCatalog.Find(
            organizationCountry.Code,
            organizationCountry.CountryName)?.DialCode;
        if (string.IsNullOrWhiteSpace(countryDialCode))
            throw new InvalidOperationException("A telephone country code is not configured for the selected country.");

        var orgPhone = ContactNormalizer.NormalizeTelephone(request.OrganizationTelephone, countryDialCode);
        var adminPhone = ContactNormalizer.NormalizeTelephone(request.TelephoneNumber, countryDialCode);

        if (!PasswordPolicy.IsValid(request.Password, adminEmail))
            throw new InvalidOperationException("Password does not meet policy requirements.");

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            throw new InvalidOperationException("Passwords do not match.");

        var selectedTimeZone = TimeZoneCatalog.Resolve(request.TimeZoneId, request.TimeZone ?? 3);

        // Never trust lookup IDs sent by the browser. Confirm the complete
        // country -> region -> district hierarchy against the database before
        // creating either the organization or its first administrator.
        await ValidateRegistrationLocationsAsync(request, cancellationToken);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var emailTaken = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == adminEmail, cancellationToken)
            || await _db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Email == orgEmail, cancellationToken);
        var phoneTaken = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.TelephoneNumber == adminPhone, cancellationToken)
            || await _db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Telephone == orgPhone, cancellationToken);

        if (emailTaken)
            throw new InvalidOperationException("The organization or administrator email is already registered.");

        if (phoneTaken)
            throw new InvalidOperationException("The organization or administrator telephone is already registered.");

        var orgNow = _timeZoneService.GetCurrentTime(selectedTimeZone.Id, request.TimeZone ?? 3);

        var organization = new Organization
        {
            OrganizationName = request.OrganizationName.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            CountryId = request.OrganizationCountryId,
            RegionId = request.OrganizationRegionId,
            DistrictId = request.OrganizationDistrictId,
            Email = orgEmail,
            Telephone = orgPhone,
            Disabled = false,
            Deleted = false,
            CreatedAt = orgNow,
            UpdatedAt = orgNow
        };

        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync(cancellationToken);

        var user = new User
        {
            OrganizationId = organization.OrganizationId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = adminEmail,
            TelephoneNumber = adminPhone,
            PasswordHash = string.Empty,
            IsActive = true,
            Disabled = false,
            Deleted = false,
            CountryId = request.OrganizationCountryId,
            RegionId = request.OrganizationRegionId,
            DistrictId = request.OrganizationDistrictId,
            TimeZone = TimeZoneCatalog.GetOffsetHours(selectedTimeZone.Id, request.TimeZone ?? 3),
            TimeZoneId = selectedTimeZone.Id,
            EntryDate = orgNow,
            IsOrgAdmin = true,
            FailedLoginCount = 0,
            IsLocked = false
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        user.PasswordHash = _passwordHasher.HashPassword(request.Password, user.UserId);
        user.RowSignature = _passwordHasher.ComputeRowSignature(user.UserId, user.Email, user.PasswordHash);
        await _db.SaveChangesAsync(cancellationToken);

        await PermissionSyncService.GrantAllPermissionsAsync(_db, user.UserId, cancellationToken);
        await WriteEventAsync(user.UserId, AuditEvents.TenantRegistered, client, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private async Task ValidateRegistrationLocationsAsync(
        TenantOnboardingDto request,
        CancellationToken cancellationToken)
    {
        var organizationCountry = await _db.Countries.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.OrganizationCountryId, cancellationToken);

        if (organizationCountry is null)
            throw new InvalidOperationException("The selected organization country is not available.");

        if (request.OrganizationRegionId is int organizationRegionId)
        {
            var regionMatchesCountry = await _db.Regions.AsNoTracking()
                .AnyAsync(r => r.RegionId == organizationRegionId && r.Country == organizationCountry.Id, cancellationToken);

            if (!regionMatchesCountry)
                throw new InvalidOperationException("The selected organization region is not valid for the country.");
        }

        var isUganda = organizationCountry.CountryName?.Equals("Uganda", StringComparison.OrdinalIgnoreCase) == true
            || organizationCountry.Code?.Equals("UGA", StringComparison.OrdinalIgnoreCase) == true;

        if (isUganda)
        {
            if (request.OrganizationDistrictId is not int districtId || districtId <= 0)
                throw new InvalidOperationException("District is required for organizations in Uganda.");

            var districtMatchesSelection = await _db.Districts.AsNoTracking()
                .AnyAsync(d => d.DistrictId == districtId
                    && d.Country == organizationCountry.Id
                    && (request.OrganizationRegionId == null || d.Region == request.OrganizationRegionId), cancellationToken);

            if (!districtMatchesSelection)
                throw new InvalidOperationException("The selected organization district is not valid for the location.");
        }
        else
        {
            // For organizations outside Uganda, region and district are not tracked
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
        var email = ContactNormalizer.NormalizeEmail(request.Email);
        if (!PasswordPolicy.IsValid(request.NewPassword, email))
            throw new InvalidOperationException("Password does not meet policy requirements.");

        var tokenHash = PasswordHasher.Sha256Hex(request.Token);

        var reset = await _db.PasswordResetTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .Where(t => t.TokenHash == tokenHash && t.User.Email == email)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (reset is null || reset.UsedAt is not null)
            throw new InvalidOperationException("Invalid or expired reset token.");

        var user = reset.User;
        var userNow = _timeZoneService.GetCurrentTime(user.TimeZoneId, user.TimeZone ?? 3);
        if (reset.ExpiresAt < userNow)
            throw new InvalidOperationException("Invalid or expired reset token.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword, user.UserId);
        user.RowSignature = _passwordHasher.ComputeRowSignature(user.UserId, user.Email, user.PasswordHash);
        reset.UsedAt = userNow;

        await RevokeAllLiveSessionsAsync(user.UserId, cancellationToken);
        await WriteEventAsync(user.UserId, AuditEvents.PasswordReset, client, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        var hash = PasswordHasher.Sha256Hex(refreshToken);

        var session = await _db.AuditTrails
            .IgnoreQueryFilters()
            .Include(a => a.User).ThenInclude(u => u.Organization)
            .FirstOrDefaultAsync(a => a.RefreshTokenHash == hash, cancellationToken);

        if (session is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        var user = session.User;
        var now = _timeZoneService.GetCurrentTime(user.TimeZoneId, user.TimeZone ?? 3);

        if (session.ExpiryDate < now)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        if (session.IsRevoked)
        {
            await RevokeAllLiveSessionsAsync(session.UserId, cancellationToken);
            await WriteEventAsync(session.UserId, AuditEvents.RefreshReuseDetected, client, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (user.IsLocked)
            throw new AccountLockedException();

        if (!_passwordHasher.VerifyRowSignature(user.UserId, user.Email, user.PasswordHash, user.RowSignature))
        {
            user.IsLocked = true;
            user.LockedDate = now;
            await WriteEventAsync(user.UserId, AuditEvents.TamperDetected, client, cancellationToken);
            await RevokeAllLiveSessionsAsync(user.UserId, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            throw new TamperDetectedException();
        }

        if (user.Disabled || user.Deleted || !user.IsActive
            || user.Organization.Disabled || user.Organization.Deleted)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        session.IsRevoked = true;
        session.RevokedDate = now;

        return await IssueTokensAsync(user, AuditEvents.TokenRefresh, client, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        var hash = PasswordHasher.Sha256Hex(refreshToken);
        var session = await _db.AuditTrails
            .IgnoreQueryFilters()
            .Include(a => a.User)
            .FirstOrDefaultAsync(a =>
                a.RefreshTokenHash == hash
                && !a.IsRevoked
                && a.RefreshTokenHash != AuditEvents.EventTokenMarker, cancellationToken);

        if (session is null)
            return;

        var now = _timeZoneService.GetCurrentTime(session.User?.TimeZoneId, session.User?.TimeZone ?? 3);
        session.IsRevoked = true;
        session.RevokedDate = now;
        await WriteEventAsync(session.UserId, AuditEvents.Logout, client, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // Break-glass: if the sole org admin is locked, unlock via SQL:
    // UPDATE Users SET IsLocked=0, FailedLoginCount=0, LockedDate=NULL, LockoutUntil=NULL WHERE UserId=@id AND IsOrgAdmin=1;
    public async Task UnlockUserAsync(int targetUserId, int actingAdminUserId, ClientRequestInfo client, CancellationToken cancellationToken = default)
    {
        var admin = await _db.Users.FirstOrDefaultAsync(u => u.UserId == actingAdminUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException();

        var target = await _db.Users.FirstOrDefaultAsync(u => u.UserId == targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        if (target.OrganizationId != admin.OrganizationId)
            throw new UnauthorizedAccessException();

        target.IsLocked = false;
        target.FailedLoginCount = 0;
        target.LockedDate = null;
        target.LockoutUntil = null;

        await WriteEventAsync(
            target.UserId,
            $"{AuditEvents.Unlocked} | by:{actingAdminUserId}",
            client,
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task RequestPasswordResetCoreAsync(
        string rawEmail,
        bool enforceHourlyLimit,
        ClientRequestInfo client,
        CancellationToken cancellationToken)
    {
        var email = ContactNormalizer.NormalizeEmail(rawEmail);
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Always succeed from the caller's perspective.
        if (user is null)
            return;

        var userNow = _timeZoneService.GetCurrentTime(user.TimeZoneId, user.TimeZone ?? 3);
        var since = userNow.AddHours(-1);
        if (enforceHourlyLimit)
        {
            var recent = await _db.PasswordResetTokens
                .IgnoreQueryFilters()
                .CountAsync(t => t.UserId == user.UserId && t.CreatedAt >= since, cancellationToken);

            if (recent >= 3)
                throw new InvalidOperationException("Too many reset requests. Try again later.");
        }

        var unused = await _db.PasswordResetTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == user.UserId && t.UsedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in unused)
            token.UsedAt = userNow;

        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = PasswordHasher.Sha256Hex(raw);

        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.UserId,
            TokenHash = hash,
            ExpiresAt = userNow.AddMinutes(30),
            CreatedAt = userNow
        });

        await WriteEventAsync(user.UserId, AuditEvents.PasswordResetRequested, client, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var url = $"{_appOptions.BaseUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(raw)}";
        await _emailService.SendPasswordResetAsync(email, url, cancellationToken);
    }

    private async Task<AuthResponseDto> IssueTokensAsync(
        User user,
        string description,
        ClientRequestInfo client,
        CancellationToken cancellationToken)
    {
        var claims = await _claimsFactory.CreateClaimsAsync(user, cancellationToken);
        var (accessToken, _, accessExpires) = _tokenGenerator.CreateAccessToken(claims);
        var (refreshToken, refreshExpires) = _tokenGenerator.CreateRefreshToken();

        var userNow = _timeZoneService.GetCurrentTime(user.TimeZoneId, user.TimeZone ?? 3);

        _db.AuditTrails.Add(new AuditTrail
        {
            UserId = user.UserId,
            AccessTokenHash = PasswordHasher.Sha256Hex(accessToken),
            RefreshTokenHash = PasswordHasher.Sha256Hex(refreshToken),
            ClientType = Truncate(client.ClientType, 100),
            DeviceIdentifier = Truncate(client.DeviceIdentifier, 255),
            IpAddress = Truncate(client.IpAddress, 50),
            UserAgent = Truncate(client.UserAgent, 500),
            CountryId = null,
            EntryDate = userNow,
            TimeZoneId = user.TimeZoneId,
            ExpiryDate = userNow.AddHours(_jwtOptions.RefreshTokenHours),
            IsRevoked = false,
            Description = Truncate(description, 255)!
        });

        await _db.SaveChangesAsync(cancellationToken);

        var permissions = await _claimsFactory.GetPermissionCodesAsync(user.UserId, cancellationToken);

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
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.TimeZoneId, u.TimeZone })
            .FirstOrDefaultAsync(cancellationToken);

        var now = _timeZoneService.GetCurrentTime(user?.TimeZoneId, user?.TimeZone ?? 3);

        _db.AuditTrails.Add(new AuditTrail
        {
            UserId = userId,
            AccessTokenHash = AuditEvents.EventTokenMarker,
            RefreshTokenHash = AuditEvents.EventTokenMarker,
            ClientType = Truncate(client.ClientType, 100),
            DeviceIdentifier = Truncate(client.DeviceIdentifier, 255),
            IpAddress = Truncate(client.IpAddress, 50),
            UserAgent = Truncate(client.UserAgent, 500),
            CountryId = null,
            EntryDate = now,
            TimeZoneId = user?.TimeZoneId,
            ExpiryDate = now,
            IsRevoked = true,
            RevokedDate = now,
            Description = Truncate(description, 255)!
        });

        await Task.CompletedTask;
    }

    private async Task RevokeAllLiveSessionsAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.TimeZoneId, u.TimeZone })
            .FirstOrDefaultAsync(cancellationToken);

        var now = _timeZoneService.GetCurrentTime(user?.TimeZoneId, user?.TimeZone ?? 3);
        var live = await _db.AuditTrails
            .IgnoreQueryFilters()
            .Where(a =>
                a.UserId == userId
                && !a.IsRevoked
                && a.ExpiryDate > now
                && a.RefreshTokenHash != AuditEvents.EventTokenMarker)
            .ToListAsync(cancellationToken);

        foreach (var row in live)
        {
            row.IsRevoked = true;
            row.RevokedDate = now;
        }
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
