using dymaptic.Blazor.StateManagement;
using dymaptic.GeoBlazor.Pro;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Serilog;
using ShipmentTracker.Client;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting WebAssembly application");
    var builder = WebAssemblyHostBuilder.CreateDefault(args);
    builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
    builder.Services.AddAuthorizationCore();
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddAuthenticationStateDeserialization();

    builder.Services.AddGeoBlazorPro(builder.Configuration);
    builder.Services.AddClientStateManagement([
        typeof(Shipment),
        typeof(PickupRequestModel),
        typeof(MonthlyStat),
        typeof(CompanySettings)
    ], 1);
    await builder.Build().RunAsync();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}