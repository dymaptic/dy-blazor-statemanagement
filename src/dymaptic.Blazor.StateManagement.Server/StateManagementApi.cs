using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;


namespace dymaptic.Blazor.StateManagement.Server;

public static class StateManagementApi
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        // find all types that implement IStateManager<T>
        IEnumerable<IStateManager> stateManagerTypes = app.Services.GetServices<IStateManager>();
        Type apiType = typeof(StateManagementApi);
        MethodInfo restGetMethod = apiType.GetMethod(nameof(RestGet), BindingFlags.Static | BindingFlags.Public)!;
        MethodInfo restPostMethod = apiType.GetMethod(nameof(RestPost), BindingFlags.Static | BindingFlags.Public)!;
        MethodInfo restPutMethod = apiType.GetMethod(nameof(RestPut), BindingFlags.Static | BindingFlags.Public)!;
        MethodInfo restDeleteMethod = apiType.GetMethod(nameof(RestDelete), BindingFlags.Static | BindingFlags.Public)!;
        MethodInfo restGetAllMethod = apiType.GetMethod(nameof(RestGetAll), BindingFlags.Static | BindingFlags.Public)!;

        foreach (IStateManager stateManager in stateManagerTypes)
        {
            Type modelType = stateManager.ModelType;
            RouteGroupBuilder stateGroup = app.MapGroup($"api/state/{modelType.Name.ToLowerInvariant()}");
            MethodInfo restGet = restGetMethod.MakeGenericMethod(modelType);
            MethodInfo restPost = restPostMethod.MakeGenericMethod(modelType);
            MethodInfo restPut = restPutMethod.MakeGenericMethod(modelType);
            MethodInfo restDelete = restDeleteMethod.MakeGenericMethod(modelType);
            MethodInfo restGetAll = restGetAllMethod.MakeGenericMethod(modelType);
            stateGroup.MapGet("{id:guid}", 
                (Guid id, IServiceProvider sp, CancellationToken ct) => restGet.Invoke(null, [id, sp, ct]));
            stateGroup.MapPost("/", 
                (HttpContext ctx, IServiceProvider sp, CancellationToken ct) => restPost.Invoke(null, [ctx, sp, ct]));
            stateGroup.MapPut("/", 
                (HttpContext ctx, IServiceProvider sp, CancellationToken ct) => restPut.Invoke(null, [ctx, sp, ct]));
            stateGroup.MapDelete("{id:guid}", 
                (Guid id, IServiceProvider sp, CancellationToken ct) => restDelete.Invoke(null, [id, sp, ct]));
            stateGroup.MapGet("/", 
                (IServiceProvider sp, CancellationToken ct) => restGetAll.Invoke(null, [sp, ct]));
        }
        
        

        return app;
    }
    
    public static async Task<IResult> RestGet<T>([FromRoute]Guid id, IServiceProvider serviceProvider, 
        CancellationToken cancellationToken = default) where T : StateRecord
    {
        IStateManager<T> stateManager = serviceProvider.GetRequiredService<IStateManager<T>>();
        T result = await stateManager.Load(id, cancellationToken);
        return Results.Ok(result);
    }
    
    public static async Task<IResult> RestPost<T>(HttpContext httpContext, IServiceProvider serviceProvider, 
        CancellationToken cancellationToken = default) where T : StateRecord
    {
        T model = (await httpContext.Request.ReadFromJsonAsync<T>(cancellationToken: cancellationToken))!;
        IStateManager<T> stateManager = serviceProvider.GetRequiredService<IStateManager<T>>();
        T result = await stateManager.Save(model, cancellationToken);
        return Results.Created($"/api/state/{result.Id}", result);
    }
    
    public static async Task<IResult> RestPut<T>(HttpContext httpContext, IServiceProvider serviceProvider, 
        CancellationToken cancellationToken = default) where T : StateRecord
    {
        T model = (await httpContext.Request.ReadFromJsonAsync<T>(cancellationToken: cancellationToken))!;
        IStateManager<T> stateManager = serviceProvider.GetRequiredService<IStateManager<T>>();
        T result = await stateManager.Update(model, cancellationToken);
        return Results.Ok(result);
    }
    
    public static async Task<IResult> RestDelete<T>(Guid id, IServiceProvider serviceProvider, 
        CancellationToken cancellationToken = default) where T : StateRecord
    {
        IStateManager<T> stateManager = serviceProvider.GetRequiredService<IStateManager<T>>();
        await stateManager.Delete(id, cancellationToken);
        return Results.NoContent();
    }
    
    public static async Task<IResult> RestGetAll<T>(IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) where T : StateRecord
    {
        IStateManager<T> stateManager = serviceProvider.GetRequiredService<IStateManager<T>>();
        IEnumerable<T> results = await stateManager.LoadAll(cancellationToken);
        return Results.Ok(results);
    }
}