using dymaptic.Blazor.StateManagement.Server;
using Microsoft.EntityFrameworkCore;
using ShipmentTracker.Client;

namespace ShipmentTracker;

public class ShipmentDbContext(DbContextOptions options, 
    IServiceProvider serviceProvider) : StateManagementDbContext(options, serviceProvider)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Shipment>()
            .Property(s => s.Status)
            .HasConversion(s => (int)s, s => (ShipmentStatus)s);
    }    
}