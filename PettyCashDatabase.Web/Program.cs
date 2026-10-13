using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PettyCashDatabase.Shared.Interfaces;
using PettyCashDatabase.Shared.Services;
using PettyCashDatabase.Web.Authentication;
using FormFactorService = PettyCashDatabase.Web.Services.FormFactor;
using PettyCashDatabase.Shared.Authorization;
using PettyCashDatabase.Web.Components;
using PettyCashDatabase.Web.Data;
using PettyCashDatabase.Web.Middlewares;
using PettyCashDatabase.Web.Options;
using PettyCashDatabase.Web.Services;

var builder = WebApplication.CreateBuilder(args);

ValidateSecurityKeys(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Development diagnostics make circuit failures actionable without exposing
        // exception details to users in production.
        options.DetailedErrors = builder.Environment.IsDevelopment();
    })
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<IFormFactor, FormFactorService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ILocationLookupService, LocationLookupService>();
builder.Services.AddSingleton<IUserTimeZoneService, UserTimeZoneService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISessionLockState, SessionLockState>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddScoped<JwtTokenGenerator>();
builder.Services.AddScoped<CustomClaimsFactory>();
builder.Services.AddScoped<IServerAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<PermissionSyncService>();

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAssertion(context =>
            context.Resource is HttpContext httpContext &&
            (httpContext.Request.Path.StartsWithSegments("/_framework") ||
             context.User.Identity?.IsAuthenticated == true))
        .Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Blazor WASM interactive services (server host also serves interactive components)
builder.Services.AddScoped<PettyCashDatabase.Web.Client.Authentication.WebTokenStorage>();
builder.Services.AddScoped<ITokenStorage>(sp => sp.GetRequiredService<PettyCashDatabase.Web.Client.Authentication.WebTokenStorage>());
builder.Services.AddScoped<AuthenticationStateProvider, PettyCashDatabase.Shared.Authentication.CustomAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddTransient<AuthHeaderHandler>();

builder.Services.AddHttpClient("AuthApi", (sp, client) =>
{
    var baseUrl = builder.Configuration["App:BaseUrl"] ?? "https://localhost:7223";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Type", "Web");
});

// Interactive Server components call the server services directly. The WebAssembly
// client has its own AuthHttpService registration and continues using the API.
builder.Services.AddScoped<IAuthService, ServerAuthServiceAdapter>();

builder.Services.AddHttpClient("Api", (sp, client) =>
{
    var baseUrl = builder.Configuration["App:BaseUrl"] ?? "https://localhost:7223";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Type", "Web");
}).AddHttpMessageHandler<AuthHeaderHandler>();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapControllers();

// Blazor UI auth is enforced by AuthorizeRouteView; API uses FallbackPolicy + [AllowAnonymous].
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(PettyCashDatabase.Shared._Imports).Assembly,
        typeof(PettyCashDatabase.Web.Client._Imports).Assembly)
    .AllowAnonymous();

app.Run();

static void ValidateSecurityKeys(IConfiguration configuration)
{
    foreach (var name in new[] { "PETTYCASH_PEPPER_KEY", "PETTYCASH_ROWSIGN_KEY" })
    {
        var value = Environment.GetEnvironmentVariable(name) ?? configuration[name];
        if (string.IsNullOrWhiteSpace(value) || value.Length < 32)
            throw new InvalidOperationException(
                $"{name} must be set and at least 32 characters (env var or user-secrets).");
    }
}
