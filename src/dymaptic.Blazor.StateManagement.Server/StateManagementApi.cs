using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;


namespace dymaptic.Blazor.StateManagement.Server;

public static class StateManagementApi
{
    public static WebApplication MapStateManagementEndpoints(this WebApplication app
    )
    {
        ILogger logger = app.Logger;
        logger.LogInformation("Mapping state management endpoints...");
        using IServiceScope scope = app.Services.CreateScope();
        // find all types that implement IStateManager<T>
        IEnumerable<IStateManager> stateManagerTypes = scope.ServiceProvider.GetServices<IStateManager>();
        Type apiType = typeof(StateManagementApi);
        MethodInfo restGetMethod = apiType.GetMethod(nameof(RestGet), BindingFlags.Static | BindingFlags.Public)!;
        MethodInfo restPostMethod = apiType.GetMethod(nameof(RestPost), BindingFlags.Static | BindingFlags.Public)!;
        MethodInfo restPutMethod = apiType.GetMethod(nameof(RestPut), BindingFlags.Static | BindingFlags.Public)!;
        MethodInfo restDeleteMethod = apiType.GetMethod(nameof(RestDelete), BindingFlags.Static | BindingFlags.Public)!;
        MethodInfo restGetAllMethod = apiType.GetMethod(nameof(RestGetAll), BindingFlags.Static | BindingFlags.Public)!;
        MethodInfo restSaveAllMethod = apiType.GetMethod(nameof(RestPostAll), BindingFlags.Static | BindingFlags.Public)!;
        MethodInfo restSearchMethod = apiType.GetMethod(nameof(RestSearch), BindingFlags.Static | BindingFlags.Public)!;

        foreach (IStateManager stateManager in stateManagerTypes)
        {
            Type modelType = stateManager.ModelType;
            logger.LogInformation("Mapping endpoints for model type: {ModelType}", modelType.Name);
            RouteGroupBuilder stateGroup = app.MapGroup($"api/state/{modelType.Name.ToLowerInvariant()}")
                .RequireAuthorization();
            MethodInfo restGet = restGetMethod.MakeGenericMethod(modelType);
            MethodInfo restPost = restPostMethod.MakeGenericMethod(modelType);
            MethodInfo restPut = restPutMethod.MakeGenericMethod(modelType);
            MethodInfo restDelete = restDeleteMethod.MakeGenericMethod(modelType);
            MethodInfo restGetAll = restGetAllMethod.MakeGenericMethod(modelType);
            MethodInfo restSaveAll = restSaveAllMethod.MakeGenericMethod(modelType);
            MethodInfo restSearch = restSearchMethod.MakeGenericMethod(modelType);
            stateGroup.MapGet("{id:guid}", 
                (Guid id, IServiceProvider sp, CancellationToken ct) => restGet.Invoke(null, [id, sp, ct]));
            stateGroup.MapPost("/", 
                (HttpContext ctx, IServiceProvider sp, CancellationToken ct) => restPost.Invoke(null, [ctx, sp, ct]));
            stateGroup.MapPut("/", 
                (HttpContext ctx, IServiceProvider sp, CancellationToken ct) => restPut.Invoke(null, [ctx, sp, ct]));
            stateGroup.MapDelete("{id:guid}", 
                (Guid id, IServiceProvider sp, CancellationToken ct) => restDelete.Invoke(null, [id, sp, ct]));
            stateGroup.MapGet("/", 
                (string query, IServiceProvider sp, CancellationToken ct) => restGetAll.Invoke(null, [query, sp, ct]));
            stateGroup.MapPost("all", 
                (HttpContext ctx, IServiceProvider sp, CancellationToken ct) => restSaveAll.Invoke(null, [ctx, sp, ct]));
            stateGroup.MapGet("search",
                (string query, IServiceProvider sp, CancellationToken ct) => 
                    restSearch.Invoke(null, [query, sp, ct]));
        }
        
        logger.LogInformation("State management endpoints mapped successfully.");
        return app;
    }
    
    public static async Task<IResult> RestGet<T>([FromRoute]Guid id, IServiceProvider serviceProvider, 
        CancellationToken cancellationToken = default) where T : StateRecord
    {
        IStateManager<T> stateManager = serviceProvider.GetRequiredService<IStateManager<T>>();
        T result = await stateManager.Load(id, cancellationToken);
        return Results.Ok(result);
    }
    
    public static async Task<IResult> RestSearch<T>(string queryString, IServiceProvider serviceProvider, 
        CancellationToken cancellationToken = default) where T : StateRecord
    {
        IStateManager<T> stateManager = serviceProvider.GetRequiredService<IStateManager<T>>();
        Dictionary<string, string> query = ParseQueryString(queryString) 
            ?? throw new ArgumentException("Query string cannot be null or empty.", nameof(queryString));
        T result = await stateManager.Search(query, cancellationToken);
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
    
    public static async Task<IResult> RestGetAll<T>(string query, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) where T : StateRecord
    {
        IStateManager<T> stateManager = serviceProvider.GetRequiredService<IStateManager<T>>();
        Dictionary<string, string>? queryParams = ParseQueryString(query);
        List<T> results = await stateManager.LoadAll(queryParams, cancellationToken);
        return Results.Ok(results);
    }
    
    public static async Task<IResult> RestPostAll<T>(HttpContext httpContext, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) where T : StateRecord
    {
        List<T> models = (await httpContext.Request.ReadFromJsonAsync<List<T>>(cancellationToken: cancellationToken))!;
        IStateManager<T> stateManager = serviceProvider.GetRequiredService<IStateManager<T>>();
        List<T> results = await stateManager.SaveAll(models, cancellationToken);
        return Results.Ok(results);
    }
    
    private static Dictionary<string, string>? ParseQueryString(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return query.Split('&')
            .Select(part => part.Split('='))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1]);
    }
}