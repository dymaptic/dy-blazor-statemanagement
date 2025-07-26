using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;


namespace dymaptic.Blazor.StateManagement;

public class ClientStateManager<T>(HttpClient httpClient, 
    ILogger<ClientStateManager<T>> logger)
    : IStateManager<T> where T: StateRecord
{
    public async Task<T> Load(Guid id, CancellationToken cancellationToken = default)
    {
        var url = $"{_apiBaseUrl}/{id}";

        T? result = await httpClient.GetFromJsonAsync<T>(url, cancellationToken);

        if (result is not null)
        {
            return result;
        }
        
        throw new InvalidOperationException($"Failed to load state record with ID {id}");
    }

    public async Task<T> Save(T model, CancellationToken cancellationToken = default)
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

    public async Task<T> Update(T model, CancellationToken cancellationToken = default)
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

    public async Task Delete(Guid id, CancellationToken cancellationToken = default)
    {
        string url = $"{_apiBaseUrl}/{id}";

        HttpResponseMessage response = await httpClient.DeleteAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new InvalidOperationException($"Failed to delete state record with ID {id}");
    }

    public async Task<IEnumerable<T>> LoadAll(CancellationToken cancellationToken = default)
    {
        string url = _apiBaseUrl;

        IEnumerable<T>? results = await httpClient.GetFromJsonAsync<IEnumerable<T>>(url, cancellationToken);

        if (results is not null)
        {
            return results;
        }

        throw new InvalidOperationException("Failed to load state records");
    }
    
    public Type ModelType => typeof(T);

    private readonly string _apiBaseUrl = $"api/state/{typeof(T).Name.ToLowerInvariant()}";
}