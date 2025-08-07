using dymaptic.Blazor.StateManagement.Server;
using dymaptic.GeoBlazor.Pro;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShipmentTracker;
using ShipmentTracker.Client;
using ShipmentTracker.Client.Pages;
using ShipmentTracker.Components.Account;
using App = ShipmentTracker.Components.App;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ShipmentDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddGeoBlazorPro(builder.Configuration);
builder.Services.AddServerStateManagement<ShipmentDbContext>([
    typeof(Shipment),
    typeof(PickupRequestModel),
    typeof(MonthlyStat),
    typeof(CompanySettings)
], 1, options =>
{
    options.UseSqlite("Data Source=shipmenttracker.db");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StateManagementDbContext>();
    if (dbContext.Database.EnsureCreated())
    {
        // Seed the database with initial data if needed
        var months = new[] { "January", "February", "March", "April", "May", "June" };
        dbContext.AddRange(months.Select((month, index) => new MonthlyStat
        {
            Month = month,
            TotalShipments = Random.Shared.Next(800, 1200),
            Revenue = Random.Shared.Next(50000, 80000),
            OnTimeRate = Random.Shared.Next(90, 98)
        }));
        dbContext.SaveChanges();
        dbContext.AddRange(Repository.GetShipments());
        dbContext.SaveChanges();
        
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapStaticAssets();
app.UseAntiforgery();

app.MapStateManagementEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Inventory).Assembly);

app.Run();