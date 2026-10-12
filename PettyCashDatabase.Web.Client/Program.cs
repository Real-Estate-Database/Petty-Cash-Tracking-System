using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PettyCashDatabase.Shared.Interfaces;
using PettyCashDatabase.Shared.Services;
using PettyCashDatabase.Shared.Authentication;
using PettyCashDatabase.Web.Client.Authentication;
using PettyCashDatabase.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddScoped<ILocationLookupService, ApiLocationLookupService>();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<WebTokenStorage>();
builder.Services.AddScoped<ITokenStorage>(sp => sp.GetRequiredService<WebTokenStorage>());
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddTransient<AuthHeaderHandler>();

var apiBase = builder.Configuration["ApiBaseUrl"]
    ?? builder.HostEnvironment.BaseAddress;

builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri(apiBase);
    client.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Type", "Web");
});

builder.Services.AddHttpClient<IAuthService, AuthHttpService>(client =>
{
    client.BaseAddress = new Uri(apiBase);
    client.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Type", "Web");
});

builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBase);
    client.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Type", "Web");
}).AddHttpMessageHandler<AuthHeaderHandler>();

await builder.Build().RunAsync();
