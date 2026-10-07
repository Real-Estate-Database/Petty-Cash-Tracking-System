using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PettyCashDatabase.Shared.Services;
using PettyCashDatabase.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the PettyCashDatabase.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

await builder.Build().RunAsync();
