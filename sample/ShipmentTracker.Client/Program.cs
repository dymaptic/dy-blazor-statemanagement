using dymaptic.Blazor.StateManagement;
using dymaptic.GeoBlazor.Pro;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ShipmentTracker.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
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
