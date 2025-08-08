using dymaptic.Blazor.StateManagement.Interfaces;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace dymaptic.Blazor.StateManagement;

public class ClientStateManager<T>(HttpClient httpClient, IndexedDb indexedDb, TimeProvider timeProvider,
    ILogger<ClientStateManager<T>> logger, IConfiguration configuration)
    : IStateManager<T> where T: StateRecord
{
    public async Task Initialize(string userId)
    {
        _userId = userId;
        await indexedDb.Initialize();
        IsInitialized = true;
    }

    public bool IsInitialized { get; private set; }
    
    public async ValueTask<T> New(CancellationToken cancellationToken = default)
    {
        DateTime time = timeProvider.GetUtcNow().DateTime;
        T newModel = Activator.CreateInstance<T>() with
        {
            Id = Guid.NewGuid(), 
            LastUpdatedUtc = time, 
            CreatedUtc = time
        };
        
        await ResetLocalCacheAndStacks(cancellationToken);

        return newModel;
    }

    public async ValueTask<T> Load(Guid id, CancellationToken cancellationToken = default)
    {
        T? model = await LoadFromIndexedDb(id, cancellationToken);

        if (model is null)
        {
            await ResetLocalCacheAndStacks(cancellationToken);
            var url = $"{_apiBaseUrl}/{id}";
            model = await httpClient.GetFromJsonAsync<T>(url, cancellationToken);
            if (model is null)
            {
                throw new InvalidOperationException($"State record with ID {id} not found.");
            }
        }

        _undoStack.Clear();
        _redoStack.Clear();
        await SaveToIndexedDb(model, cancellationToken);
        return model;
    }
    
    public virtual async ValueTask<T> Track(T model, CancellationToken cancellationToken = default)
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
        await SaveToIndexedDb(snapShot, cancellationToken);
        return model;
    }

    public async ValueTask<T?> Search(Dictionary<string, string> queryParams, CancellationToken cancellationToken = default)
    {
        string queryString = BuildQueryString(queryParams);
        string url = $"{_apiBaseUrl}/search?{queryString}";
        
        T? result = await httpClient.GetFromJsonAsync<T>(url, cancellationToken);

        if (result is not null)
        {
            _undoStack.Clear();
            _redoStack.Clear();
            await SaveToIndexedDb(result, cancellationToken);
        }
        
        return result;
    }

    public async ValueTask<T> Save(T model, CancellationToken cancellationToken = default)
    {
        Task<HttpResponseMessage> response = httpClient.PostAsJsonAsync(_apiBaseUrl, model, cancellationToken);

        if (response.IsCompletedSuccessfully)
        {
            T? result = await response.Result.Content.ReadFromJsonAsync<T>(cancellationToken);
            
            if (result is not null)
            {
                await SaveToIndexedDb(result, cancellationToken);
                return result;
            }
        }

        throw new InvalidOperationException("Failed to save state record");
    }

    public async ValueTask<T> Update(T model, CancellationToken cancellationToken = default)
    {
        if (model == _previousState)
        {
            return model;
        }

        _previousState = model with { };

        DateTime time = timeProvider.GetUtcNow().DateTime;

        T snapShot = model with { LastUpdatedUtc = time };
        _undoStack.Push(snapShot);
        await SaveToIndexedDb(snapShot, cancellationToken);
            
        HttpResponseMessage response = await httpClient.PutAsJsonAsync(_apiBaseUrl, model, cancellationToken);

        response.EnsureSuccessStatusCode();
        T? result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            
        if (result is not null)
        {
            return result;
        }
            
        return snapShot;
    }

    public async ValueTask Delete(Guid id, CancellationToken cancellationToken = default)
    {
        string url = $"{_apiBaseUrl}/{id}";

        HttpResponseMessage response = await httpClient.DeleteAsync(url, cancellationToken);

        response.EnsureSuccessStatusCode();
        _undoStack.Clear();
        _redoStack.Clear();
        await indexedDb.Delete<T>(id, cancellationToken);
    }

    public async ValueTask<List<T>> LoadAll(Dictionary<string, string>? queryParams,
        CancellationToken cancellationToken = default)
    {
        string url = _apiBaseUrl;
        if (queryParams is not null && queryParams.Any())
        {
            url += $"?{BuildQueryString(queryParams)}";
        }

        List<T>? results = await httpClient.GetFromJsonAsync<List<T>>(url, cancellationToken);

        if (results is not null)
        {
            return results;
        }

        throw new InvalidOperationException("Failed to load state records");
    }
    
    public async ValueTask<List<T>> SaveAll(List<T> models, CancellationToken cancellationToken = default)
    {
        string url = $"{_apiBaseUrl}/all";

        Task<HttpResponseMessage> response = httpClient.PostAsJsonAsync(url, models, cancellationToken);

        if (response.IsCompletedSuccessfully)
        {
            List<T>? results = await response.Result.Content.ReadFromJsonAsync<List<T>>(cancellationToken);
            
            if (results is not null)
            {
                return results;
            }
        }

        throw new InvalidOperationException("Failed to save state records");
    }
    
    public async ValueTask<T?> GetMostRecent(string userId, CancellationToken cancellationToken = default)
    {
        await Initialize(userId);
        CacheStorageRecord<T>[]? cachedRecords =
            await indexedDb.GetAll<CacheStorageRecord<T>>(cancellationToken);
        // reload from cache
        return cachedRecords?
                .OrderByDescending(r => r.TimeStamp)
                .FirstOrDefault(r => r.UserId == userId)?.Item;
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
        
        await SaveToIndexedDb(lastItem, cancellationToken);

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
        
        await SaveToIndexedDb(lastItem, cancellationToken);
        return lastItem;
    }
    
    private string BuildQueryString(Dictionary<string, string> queryParams)
    {
        return string.Join("&", queryParams.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
    }
    
    private async Task SaveToIndexedDb(T record, CancellationToken cancellationToken)
    {
        logger.LogInformation("Saving record {record}", record);
        await indexedDb.Put(new CacheStorageRecord<T>(record, record.Id, _userId!, timeProvider.GetUtcNow().DateTime), 
            cancellationToken);
    }
    
    private async Task<T?> LoadFromIndexedDb(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            CacheStorageRecord<T>? cachedRecord = await indexedDb.Get<CacheStorageRecord<T>>(id, cancellationToken);

            if (cachedRecord is not null && cachedRecord.UserId == _userId
                                         && cachedRecord.TimeStamp + _cacheDuration < timeProvider.GetUtcNow().DateTime)
            {
                logger.LogInformation("Loading from indexed DB");
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
                await indexedDb.Delete<CacheStorageRecord<T>>(lastItem.Id, cancellationToken);
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
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(configuration.GetValue("StateManagement:_cacheDuration", 5));
    private readonly string _apiBaseUrl = $"api/state/{typeof(T).Name.ToLowerInvariant()}";
    private StateRecord? _previousState;
    private string? _userId;
}