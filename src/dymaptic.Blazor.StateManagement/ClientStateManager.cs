using dymaptic.Blazor.StateManagement.Interfaces;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace dymaptic.Blazor.StateManagement;

public class ClientStateManager<T>(HttpClient httpClient, IndexedDb indexedDb, TimeProvider timeProvider,
    ILogger<ClientStateManager<T>> logger, IConfiguration configuration)
    : IStateManager<T> where T: StateRecord
{
    public void Initialize(string userId)
    {
        _userId = userId;
    }
    
    public async ValueTask<T> Load(Guid id, CancellationToken cancellationToken = default)
    {
        var url = $"{_apiBaseUrl}/{id}";

        T? result = await httpClient.GetFromJsonAsync<T>(url, cancellationToken);

        if (result is not null)
        {
            return result;
        }
        
        throw new InvalidOperationException($"Failed to load state record with ID {id}");
    }
    
    public virtual async ValueTask<T> Track(T model, CancellationToken cancellationToken = default)
    {
        try
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to track changes");

            return model;
        }
    }

    public async ValueTask<T> Search(Dictionary<string, string> queryParams, CancellationToken cancellationToken = default)
    {
        string queryString = BuildQueryString(queryParams);
        string url = $"{_apiBaseUrl}/search?{queryString}";
        
        T? result = await httpClient.GetFromJsonAsync<T>(url, cancellationToken);

        if (result is not null)
        {
            return result;
        }

        throw new InvalidOperationException($"Failed to search state records with query '{queryString}'");
    }

    public async ValueTask<T> Save(T model, CancellationToken cancellationToken = default)
    {
        string url = _apiBaseUrl;

        Task<HttpResponseMessage> response = httpClient.PostAsJsonAsync(url, model, cancellationToken);

        if (response.IsCompletedSuccessfully)
        {
            T? result = await response.Result.Content.ReadFromJsonAsync<T>(cancellationToken);
            
            if (result is not null)
            {
                return result;
            }
        }

        throw new InvalidOperationException("Failed to save state record");
    }

    public async ValueTask<T> Update(T model, CancellationToken cancellationToken = default)
    {
        string url = _apiBaseUrl;

        Task<HttpResponseMessage> response = httpClient.PutAsJsonAsync(url, model, cancellationToken);

        if (response.IsCompletedSuccessfully)
        {
            T? result = await response.Result.Content.ReadFromJsonAsync<T>(cancellationToken);
            
            if (result is not null)
            {
                return result;
            }
        }

        throw new InvalidOperationException("Failed to update state record");
    }

    public async ValueTask Delete(Guid id, CancellationToken cancellationToken = default)
    {
        string url = $"{_apiBaseUrl}/{id}";

        HttpResponseMessage response = await httpClient.DeleteAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new InvalidOperationException($"Failed to delete state record with ID {id}");
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
    
    private string BuildQueryString(Dictionary<string, string> queryParams)
    {
        return string.Join("&", queryParams.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
    }
    
    private async Task SaveToIndexedDb(T record, CancellationToken cancellationToken)
    {
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
                return cachedRecord.Item;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load from IndexedDb");
        }

        return null;
    }
    
    public Type ModelType => typeof(T);
    private TimeSpan _cacheDuration = TimeSpan.FromMinutes(configuration.GetValue("StateManagement:_cacheDuration", 5));
    private readonly string _apiBaseUrl = $"api/state/{typeof(T).Name.ToLowerInvariant()}";
    private StateRecord? _previousState;
    private readonly Stack<T> _undoStack = [];
    private readonly Stack<T> _redoStack = [];
    private string? _userId;
}