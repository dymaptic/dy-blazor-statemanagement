using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using System.Reflection;
using System.Text.RegularExpressions;


// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract


namespace dymaptic.Blazor.StateManagement.Server;

public class ServerStateManager<T>(StateManagementDbContext dbContext,
    HybridCache hybridCache, IConfiguration configuration,
    TimeProvider timeProvider, ILogger<ServerStateManager<T>> logger)
    : IStateManager<T> where T : StateRecord
{
    public Task Initialize(string userId)
    {
        // Initialization logic can be added here if needed.
        // For example, setting up user-specific configurations or logging.
        IsInitialized = true;
        _userId = userId;
        return Task.CompletedTask;
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
        logger.LogInformation("Tracking changes at {Time} for model {Id}", time, model.Id);

        T snapShot = model with { LastUpdatedUtc = time };
        _undoStack.Push(snapShot);
        await SaveToCache(snapShot, cancellationToken);
        return model;
    }
    
    public async ValueTask<T?> Search(List<SearchRecord> queryParams, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = dbContext.Set<T>();
        Type modelType = typeof(T);
        PropertyInfo[] properties = modelType.GetProperties();
        foreach (SearchRecord param in queryParams)
        {
            query = AppendQueryFilter(query, properties, param);
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

    public async ValueTask<List<T>> LoadAll(List<SearchRecord>? queryParams, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = dbContext.Set<T>();

        if (queryParams is not null)
        {
            Type modelType = typeof(T);
            PropertyInfo[] properties = modelType.GetProperties();
            foreach (SearchRecord param in queryParams)
            {
                query = AppendQueryFilter(query, properties, param);
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
    
    public async ValueTask<T?> GetMostRecent(string userId, CancellationToken cancellationToken = default)
    {
        await Initialize(userId);
        // TODO: Implement logic to retrieve the most recent record for the specified user.
        return null;
    }
    
    private async Task SaveToCache(T record, CancellationToken cancellationToken)
    {
        logger.LogInformation("Saving to cache of type {Type}", record.GetType());
        await hybridCache.SetAsync(record.Id.ToString(), 
            new CacheStorageRecord<T>(record, record.Id, _userId!, DateTime.UtcNow), 
            _cacheOptions, null, cancellationToken);
    }
    
    private async Task<T> LoadFromCache(Guid id, Func<CancellationToken, Task<T>> callback, 
        CancellationToken cancellationToken)
    {
        CacheStorageRecord<T> cachedRecord = await hybridCache
            .GetOrCreateAsync<CacheStorageRecord<T>>(id.ToString(), 
                async token =>
                {
                    T model = await callback(token);
                    return new CacheStorageRecord<T>(model, model.Id, _userId!, DateTime.UtcNow);
                },
                _cacheOptions, 
                null, 
                cancellationToken);

        if (cachedRecord.UserId == _userId)
        {
            logger.LogInformation("Loaded from cache of type {Type}", cachedRecord.Item.GetType());
            return cachedRecord.Item;
        }
        
        throw new InvalidOperationException("Cached record does not belong to the current user.");
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
    
    private IQueryable<T> AppendQueryFilter(IQueryable<T> query, PropertyInfo[] properties, SearchRecord param)
    {
        if (string.IsNullOrWhiteSpace(param.SearchValue))
        {
            return query;
        }
            
        PropertyInfo? property = properties
            .FirstOrDefault(p => p.Name.Equals(param.PropertyName, StringComparison.OrdinalIgnoreCase));

        if (property is null)
        {
            return query;
        }

        object value = GetSearchValue(param.SearchValue, property);

        switch (param.SearchOption)
        {
            case SearchOption.Equals:
                return query.Where(e => EF.Property<object>(e, param.PropertyName) == value);
            case SearchOption.Contains:
                return query.Where(e => EF.Functions.Like(
                    EF.Property<string>(e, param.PropertyName),
                    $"%{param.SearchValue}%"));
            case SearchOption.StartsWith:
                return query.Where(e => EF.Functions.Like(
                    EF.Property<string>(e, param.PropertyName),
                    $"{param.SearchValue}%"));
            case SearchOption.EndsWith:
                return query.Where(e => EF.Functions.Like(
                    EF.Property<string>(e, param.PropertyName),
                    $"%{param.SearchValue}"));
            case SearchOption.NotEquals:
                return query.Where(e => EF.Property<object>(e, param.PropertyName) != value);
            // TODO: FIX THESE COMPARISONS
            // case SearchOption.GreaterThan:
            //     if (value is not IComparable gtComparable)
            //     {
            //         logger.LogWarning("Value for GreaterThan search option must implement IComparable");
            //         return query;
            //     }
            //     return query.Where(e => EF.Property<IComparable>(e, param.PropertyName).CompareTo(gtComparable) > 0);
            // case SearchOption.LessThan:
            //     if (value is not IComparable ltComparable)
            //     {
            //         logger.LogWarning("Value for LessThan search option must implement IComparable");
            //         return query;
            //     }
            //     return query.Where(e => EF.Property<IComparable>(e, param.PropertyName).CompareTo(ltComparable) < 0);
            // case SearchOption.GreaterThanOrEqual:
            //     if (value is not IComparable gteComparable)
            //     {
            //         logger.LogWarning("Value for GreaterThanOrEqual search option must implement IComparable");
            //         return query;
            //     }
            //     return query.Where(e => EF.Property<IComparable>(e, param.PropertyName).CompareTo(gteComparable) >= 0);
            // case SearchOption.LessThanOrEqual:
            //     if (value is not IComparable lteComparable)
            //     {
            //         logger.LogWarning("Value for LessThanOrEqual search option must implement IComparable");
            //         return query;
            //     }
            //     return query.Where(e => EF.Property<IComparable>(e, param.PropertyName).CompareTo(lteComparable) <= 0);
            case SearchOption.IsNull:
                return query.Where(e => EF.Property<object>(e, param.PropertyName) == null);
            case SearchOption.IsNotNull:
                return query.Where(e => EF.Property<object>(e, param.PropertyName) != null);
            case SearchOption.In:
                var inValues = param.SearchValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => v.Trim())
                    .ToList();

                if (inValues.Count == 0)
                {
                    return query;
                }
                
                return query.Where(e => inValues.Contains(EF.Property<string>(e, param.PropertyName)));
            case SearchOption.NotIn:
                if (string.IsNullOrWhiteSpace(param.SearchValue))
                {
                    return query;
                }
                
                var notInValues = param.SearchValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => v.Trim())
                    .ToList();
                
                if (notInValues.Count == 0)
                    return query;
                
                return query.Where(e => !notInValues.Contains(EF.Property<string>(e, param.PropertyName)));
            case SearchOption.Between:
                if (string.IsNullOrWhiteSpace(param.SecondarySearchValue))
                {
                    logger.LogWarning("Between search option requires SecondarySearchValue");
                    return query;
                }
                
                object secondaryValue = GetSearchValue(param.SecondarySearchValue, property);
                
                return query.Where(e => 
                    EF.Property<IComparable>(e, param.PropertyName).CompareTo(value) >= 0 &&
                    EF.Property<IComparable>(e, param.PropertyName).CompareTo(secondaryValue) <= 0);
            case SearchOption.NotBetween:
                if (string.IsNullOrWhiteSpace(param.SecondarySearchValue))
                {
                    logger.LogWarning("Between search option requires SecondarySearchValue");
                    return query;
                }
                
                object secondaryValue2 = GetSearchValue(param.SecondarySearchValue, property);
                
                return query.Where(e => 
                    EF.Property<IComparable>(e, param.PropertyName).CompareTo(value) < 0 ||
                    EF.Property<IComparable>(e, param.PropertyName).CompareTo(secondaryValue2) > 0);
            case SearchOption.Regex:
                return query.Where(e => Regex.IsMatch(EF.Property<string>(e, param.PropertyName),
                    param.SearchValue, RegexOptions.IgnoreCase));
            default:
                logger.LogWarning("Unsupported search option: {SearchOption}", param.SearchOption);
                return query;
        }
    }
    
    private object GetSearchValue(string searchValue, PropertyInfo property)
    {
        if (property.PropertyType == typeof(string))
        {
            return searchValue;
        }
        
        // check if the property is an enum
        if (property.PropertyType.IsEnum)
        {
            try
            {
                return Enum.Parse(property.PropertyType, searchValue, true);
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "Failed to parse enum value '{Value}' for property '{PropertyName}'", 
                    searchValue, property.Name);
                throw;
            }
        }

        try
        {
            return Convert.ChangeType(searchValue, property.PropertyType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to convert value '{Value}' for property '{PropertyName}'", 
                searchValue, property.Name);
            throw;
        }
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

