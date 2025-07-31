using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace dymaptic.Blazor.StateManagement.Server;

public class StateManagementDbContext(DbContextOptions<StateManagementDbContext> options,
    IServiceProvider serviceProvider): IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        IServiceScope scope = serviceProvider.CreateScope();
        List<Type> stateRecordTypes = scope.ServiceProvider
            .GetServices<IStateManager>()
            .Select(s => s.ModelType)
            .ToList();

        foreach (Type type in stateRecordTypes)
        {
            modelBuilder.Entity(type);
        }
    }
}