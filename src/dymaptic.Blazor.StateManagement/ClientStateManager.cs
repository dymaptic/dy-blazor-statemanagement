using dymaptic.Blazor.StateManagement.Interfaces;
using System.Net.Http.Json;


namespace dymaptic.Blazor.StateManagement;

public class ClientStateManager<T>(HttpClient httpClient)
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
    
    public async Task<T> Search(Dictionary<string, string> queryParams, CancellationToken cancellationToken = default)
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

    public async Task<List<T>> LoadAll(Dictionary<string, string>? queryParams,
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
    
    public async Task<List<T>> SaveAll(List<T> models, CancellationToken cancellationToken = default)
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
    
    public Type ModelType => typeof(T);

    private readonly string _apiBaseUrl = $"api/state/{typeof(T).Name.ToLowerInvariant()}";
}