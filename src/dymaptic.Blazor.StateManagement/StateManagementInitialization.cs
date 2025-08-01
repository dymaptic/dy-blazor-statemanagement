using System.Reflection;
using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace dymaptic.Blazor.StateManagement;

public static class StateManagementInitialization
{
    public static IServiceCollection AddClientStateManagement(this IServiceCollection services,
        IList<Type> stateRecordTypes, int indexedDbVersion = 1)
    {
        services.AddIndexedDb(stateRecordTypes, indexedDbVersion);
        services.AddSingleton(TimeProvider.System);
        Type initializerType = typeof(StateManagementInitialization);
        MethodInfo addStateManagerMethod = initializerType
            .GetMethod(nameof(AddClientStateManager), BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (Type stateRecordType in stateRecordTypes)
        {
            MethodInfo genericAddStateManager = addStateManagerMethod.MakeGenericMethod(stateRecordType);
            services = (IServiceCollection)genericAddStateManager.Invoke(null, [services])!;
        }

        return services;
    }
    
    public static IServiceCollection AddIndexedDb(this IServiceCollection services,
        IList<Type> stateRecordTypes, int indexedDbVersion = 1)
    {
        List<DbObjectStore> stores = [];
        foreach (Type stateRecordType in stateRecordTypes)
        {
            Type cacheStorageRecordType = typeof(CacheStorageRecord<>).MakeGenericType(stateRecordType);
            Type cacheStorageCollectionType = typeof(CacheStorageCollectionRecord<>).MakeGenericType(stateRecordType);
            DbObjectStore store = new(cacheStorageRecordType, "ItemId", 
                [new DbIndex("itemId", "itemId", true)]);
            stores.Add(store);
            DbObjectStore collectionStore = new(cacheStorageCollectionType, "ListId", 
                [new DbIndex("listId", "listId", true)]);
            stores.Add(collectionStore);
        }
        
        services.AddScoped(sp => new IndexedDb(sp.GetRequiredService<IJSRuntime>(), 
            "stateManagementDb", indexedDbVersion, stores));
        return services;
    }
    
    private static IServiceCollection AddClientStateManager<T>(IServiceCollection services)
        where T : StateRecord
    {
        services.AddHttpClient<IStateManager<T>, ClientStateManager<T>>();
        services.AddScoped<IStateManager>(sp => sp.GetRequiredService<IStateManager<T>>());
        return services;
    }
}