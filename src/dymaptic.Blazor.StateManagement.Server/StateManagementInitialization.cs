using System.Reflection;
using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace dymaptic.Blazor.StateManagement.Server;

public static class StateManagementInitialization
{
    public static IServiceCollection AddServerStateManagement(this IServiceCollection services,
        IList<Type> stateRecordTypes, int indexedDbVersion = 1, 
        Action<DbContextOptionsBuilder>? dbContextOptionsAction = null)
    {
        services.AddIndexedDb(stateRecordTypes, indexedDbVersion);
        services.AddDbContext<StateManagementDbContext>(dbContextOptionsAction);
        Type initializerType = typeof(StateManagementInitialization);
        MethodInfo addServerStateManagerMethod = initializerType
            .GetMethod(nameof(AddServerStateManager), BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (Type stateRecordType in stateRecordTypes)
        {
            MethodInfo genericAddServerStateManager = addServerStateManagerMethod.MakeGenericMethod(stateRecordType);
            services = (IServiceCollection)genericAddServerStateManager.Invoke(null, [services])!;
        }

        return services;
    }
    
    private static IServiceCollection AddServerStateManager<T>(IServiceCollection services)
        where T : StateRecord
    {
        services.AddScoped<IStateManager<T>, ServerStateManager<T>>();
        services.AddScoped<IStateManager>(sp => sp.GetRequiredService<IStateManager<T>>());
        return services;
    }
}