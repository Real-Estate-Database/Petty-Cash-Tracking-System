using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using PettyCashDatabase.Services;
using PettyCashDatabase.Shared.Interfaces;
using PettyCashDatabase.Shared.Services;
using PettyCashDatabase.Shared.Authentication;

namespace PettyCashDatabase;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        builder.Services.AddScoped<ILocationLookupService, ApiLocationLookupService>();
        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<ITokenStorage, MauiTokenStorage>();
        builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
        builder.Services.AddTransient<AuthHeaderHandler>();

        // Configure per environment / device as needed.
        const string apiBase = "https://localhost:7223/";

        builder.Services.AddHttpClient("AuthApi", client =>
        {
            client.BaseAddress = new Uri(apiBase);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Type", "MAUI");
        });

        builder.Services.AddHttpClient<IAuthService, AuthHttpService>(client =>
        {
            client.BaseAddress = new Uri(apiBase);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Type", "MAUI");
        });

        builder.Services.AddHttpClient("Api", client =>
        {
            client.BaseAddress = new Uri(apiBase);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Type", "MAUI");
        }).AddHttpMessageHandler<AuthHeaderHandler>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
