using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using System.Reflection;
using System.Text;


namespace dymaptic.Blazor.StateManagement.Server;

public class ServerStateManager<T>(StateManagementDbContext dbContext,
    HybridCache hybridCache, IConfiguration configuration,
    TimeProvider timeProvider, ILogger<ServerStateManager<T>> logger)
    : IStateManager<T> where T : StateRecord
{
    public void Initialize(string userId)
    {
        // Initialization logic can be added here if needed.
        // For example, setting up user-specific configurations or logging.
        IsInitialized = true;
        _userId = userId;
    }

    public async ValueTask<T> New(CancellationToken cancellationToken = default)
    {
        DateTime time = DateTime.UtcNow;
        T newModel = Activator.CreateInstance<T>() with
        {
            Id = Guid.NewGuid(),
            LastUpdatedUtc = time,
            CreatedUtc = time
        };

        await ResetLocalCacheAndStacks(cancellationToken);

        return newModel;
    }

    public async ValueTask<T> Load(Guid id, CancellationToken cancellationToken)
    {
        T model = await LoadFromCache(id, async token =>
        {
            await ResetLocalCacheAndStacks(token);
            return await dbContext.Set<T>().FindAsync([id], token)
                ?? throw new InvalidOperationException($"State record with ID {id} not found.");
        }, cancellationToken);

        _undoStack.Clear();
        _redoStack.Clear();
        await SaveToCache(model, cancellationToken);
        return model;
    }

    public async ValueTask<T> Track(T model, CancellationToken cancellationToken = default)
    {
        if (model == _previousState)
        {
            logger.LogInformation("No changes detected, skipping tracking.");
            return model;
        }

        _previousState = model with { };

        DateTime time = timeProvider.GetUtcNow().DateTime;

        T snapShot = model with { LastUpdatedUtc = time };
        _undoStack.Push(snapShot);
        await SaveToCache(snapShot, cancellationToken);
        return model;
    }
    
    public async ValueTask<T?> Search(Dictionary<string, string> queryParams, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = dbContext.Set<T>();
        Type modelType = typeof(T);
        PropertyInfo[] properties = modelType.GetProperties();
        foreach (KeyValuePair<string, string> param in queryParams)
        {
            PropertyInfo? property = properties
                .FirstOrDefault(p => p.Name.Equals(param.Key, StringComparison.OrdinalIgnoreCase));

            if (property is null)
            {
                continue;
            }

            object value = param.Value;
            if (property.PropertyType != typeof(string))
            {
                try
                {
                    value = Convert.ChangeType(param.Value, property.PropertyType);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to convert value '{Value}' for property '{PropertyName}'", 
                        param.Value, param.Key);

                    continue;
                }
            }
            query = query.Where(e => EF.Property<string>(e, param.Key) == value);
        }
        
        T? result = await query.FirstOrDefaultAsync(cancellationToken);
        if (result is not null)
        {
            _undoStack.Clear();
            _redoStack.Clear();
            await SaveToCache(result, cancellationToken);
        }
        
        return result;
    }

    public async ValueTask<T> Save(T model, CancellationToken cancellationToken)
    {
        dbContext.Set<T>().Add(model);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveToCache(model, cancellationToken);

        return model;
    }

    public async ValueTask<T> Update(T model, CancellationToken cancellationToken)
    {
        if (model == _previousState)
        {
            return model;
        }

        _previousState = model with { };

        DateTime time = timeProvider.GetUtcNow().DateTime;

        T snapShot = model with { LastUpdatedUtc = time };
        _undoStack.Push(snapShot);
        await SaveToCache(snapShot, cancellationToken);
        
        dbContext.Set<T>().Update(model);
        await dbContext.SaveChangesAsync(cancellationToken);

        return model;
    }

    public async ValueTask Delete(Guid id, CancellationToken cancellationToken)
    {
        T? entity = await dbContext.Set<T>().FindAsync([id], cancellationToken);
        if (entity is null)
        {
            throw new InvalidOperationException($"State record with ID {id} not found.");
        }

        dbContext.Set<T>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await hybridCache.RemoveAsync(id.ToString(), cancellationToken);
    }

    public async ValueTask<List<T>> LoadAll(Dictionary<string, string>? queryParams, CancellationToken cancellationToken)
    {
        IQueryable<T> query = dbContext.Set<T>();

        if (queryParams is not null)
        {
            Type modelType = typeof(T);
            PropertyInfo[] properties = modelType.GetProperties();
            foreach (KeyValuePair<string, string> param in queryParams)
            {
                PropertyInfo? property = properties
                    .FirstOrDefault(p => p.Name.Equals(param.Key, StringComparison.OrdinalIgnoreCase));

                if (property is null)
                {
                    continue;
                }

                object value = param.Value;
                if (property.PropertyType != typeof(string))
                {
                    try
                    {
                        value = Convert.ChangeType(param.Value, property.PropertyType);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to convert value '{Value}' for property '{PropertyName}'", 
                            param.Value, param.Key);

                        continue;
                    }
                }
                query = query.Where(e => EF.Property<string>(e, param.Key) == value);
            }
        }

        return await query.ToListAsync(cancellationToken);
    }
    
    public async ValueTask<T?> Undo(CancellationToken cancellationToken = default)
    {
        if (_undoStack.Count == 0)
        {
            logger.LogWarning("Undo stack is empty, cannot perform undo operation.");
            return null;
        }

        T lastItem = _undoStack.Pop();
        _redoStack.Push(lastItem);
        
        await SaveToCache(lastItem, cancellationToken);

        return lastItem;
    }

    public async ValueTask<T?> Redo(CancellationToken cancellationToken = default)
    {
        if (_redoStack.Count == 0)
        {
            logger.LogWarning("Redo stack is empty, cannot perform redo operation.");
            return null;
        }

        T lastItem = _redoStack.Pop();
        _undoStack.Push(lastItem);
        
        await SaveToCache(lastItem, cancellationToken);
        return lastItem;
    }

    public bool IsInitialized { get; private set; }
    

    public async ValueTask<List<T>> SaveAll(List<T> models, CancellationToken cancellationToken = default)
    {
        dbContext.Set<T>().AddRange(models);
        await dbContext.SaveChangesAsync(cancellationToken);

        return models;
    }
    
    private async Task SaveToCache(T record, CancellationToken cancellationToken)
    {
        await hybridCache.SetAsync(record.Id.ToString(), 
            new CacheStorageRecord<T>(record, record.Id, _userId!, DateTime.UtcNow), 
            _cacheOptions, null, cancellationToken);
    }
    
    private async Task<T> LoadFromCache(Guid id, Func<CancellationToken, Task<T>> callback, 
        CancellationToken cancellationToken)
    {
        try
        {
            // The GetOrCreate pattern doesn't really fit with our needs here
            // so we always return null if the record is not found.
            CacheStorageRecord<T>? cachedRecord = await hybridCache
                .GetOrCreateAsync<CacheStorageRecord<T>?>(id.ToString(), 
                    async token =>
                    {
                        T model = await callback(token);
                        return new CacheStorageRecord<T>(model, model.Id, _userId!, DateTime.UtcNow);
                    },
                    _cacheOptions, 
                    null, 
                    cancellationToken);

            if (cachedRecord is not null && cachedRecord.UserId == _userId)
            {
                return cachedRecord.Item;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load from IndexedDb");
        }

        return null;
    }
    
    private async Task ResetLocalCacheAndStacks(CancellationToken cancellationToken)
    {
        try
        {
            if (_undoStack.Count > 0)
            {
                T lastItem = _undoStack.Pop();
                await hybridCache.RemoveAsync(lastItem.Id.ToString(), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete last item from IndexedDb");
        }

        _undoStack.Clear();
        _redoStack.Clear();
    }

    public Type ModelType => typeof(T);
    private readonly Stack<T> _undoStack = [];
    private readonly Stack<T> _redoStack = [];
    private readonly string _apiBaseUrl = $"api/state/{typeof(T).Name.ToLowerInvariant()}";
    private StateRecord? _previousState;
    private string? _userId;
    private readonly HybridCacheEntryOptions _cacheOptions = new()
    { 
        Expiration = TimeSpan.FromMinutes(configuration.GetValue("StateManagement:CacheDuration", 5))
    };
}