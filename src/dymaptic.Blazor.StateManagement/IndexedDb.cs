using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace dymaptic.Blazor.StateManagement;


public class IndexedDb(IJSRuntime jsRuntime, string databaseName, int version, IEnumerable<DbObjectStore> stores) 
    : IAsyncDisposable
{
    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return;
        }
        
        _module = await _moduleTask.Value;
        _database = await _module.InvokeAsync<IJSObjectReference>("initialize", cancellationToken, 
            databaseName, version, stores);
        
        Console.WriteLine($"IndexedDb '{databaseName}' v{version} initialized successfully");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_database is not null && _module is not null)
            {
                await _module.InvokeVoidAsync("closeDatabase", _database);
                await _database.DisposeAsync();
                _database = null;
            }
            
            if (_moduleTask.IsValueCreated)
            {
                var module = await _moduleTask.Value;
                await module.DisposeAsync();
                _module = null;
            }
        }
        catch (JSException)
        {
            // Ignore disposal errors
        }
    }

    public async Task<object?> Add<T>(T item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return await ExecuteWithErrorHandling(async () =>
            await _module!.InvokeAsync<object?>("add", cancellationToken, _database, GetStoreName<T>(), item));
    }

    public async Task Delete<T>(object key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await ExecuteWithErrorHandling(async () =>
            await _module!.InvokeVoidAsync("deleteRecord", cancellationToken, _database, GetStoreName<T>(), key));
    }
    
    public async Task<T?> Get<T>(object key, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        return await ExecuteWithErrorHandling(async () =>
            await _module!.InvokeAsync<T?>("get", cancellationToken, _database, GetStoreName<T>(), key));
    }
    
    public async Task<T[]?> GetAll<T>(CancellationToken cancellationToken = default) where T : class
    {
        return await ExecuteWithErrorHandling(async () =>
            await _module!.InvokeAsync<T[]?>("getAll", cancellationToken, _database, GetStoreName<T>()));
    }

    public async Task Put<T>(T item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await ExecuteWithErrorHandling(async () =>
            await _module!.InvokeVoidAsync("put", cancellationToken, _database, GetStoreName<T>(), item));
    }

    public async Task Clear<T>(CancellationToken cancellationToken = default)
    {
        await ExecuteWithErrorHandling(async () =>
            await _module!.InvokeVoidAsync("clearStore", cancellationToken, _database, GetStoreName<T>()));
    }
    
    public async Task<int> Count<T>(CancellationToken cancellationToken = default)
    {
        return await ExecuteWithErrorHandling(async () =>
            await _module!.InvokeAsync<int>("count", cancellationToken, _database, GetStoreName<T>()));
    }
    
    
    private void ThrowIfNotInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("IndexedDb must be initialized before use. Call Initialize() first.");
    }

    private string GetStoreName<T>() => typeof(T).GetIndexedDbStoreName();

    private async Task<T> ExecuteWithErrorHandling<T>(Func<Task<T>> operation)
    {
        ThrowIfNotInitialized();
        return await operation();
    }

    private async Task ExecuteWithErrorHandling(Func<Task> operation)
    {
        ThrowIfNotInitialized();
        await operation();
    }

    public bool IsInitialized => _module is not null && _database is not null;
    
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new (() => jsRuntime.InvokeAsync<IJSObjectReference>(
        "import", "./_content/dymaptic.Blazor.StateManagement/indexedDb.js").AsTask());
    private IJSObjectReference? _database;
    private IJSObjectReference? _module;
}

[JsonConverter(typeof(DbObjectStoreConverter))]
public record DbObjectStore(Type ObjectType, string? KeyPath, IEnumerable<DbIndex>? Indexes = null);
public record DbIndex(string Name, string? KeyPath, bool Unique = false, bool MultiEntry = false);

internal class DbObjectStoreConverter : JsonConverter<DbObjectStore>
{
    public override DbObjectStore Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, DbObjectStore value, JsonSerializerOptions options)
    {
        
        writer.WriteStartObject();
        writer.WriteString("name", value.ObjectType.GetIndexedDbStoreName());
        writer.WriteString("keyPath", value.KeyPath?.ToLowerFirstChar());
        writer.WritePropertyName("indexes");
        JsonSerializer.Serialize(writer, value.Indexes, options);
        writer.WriteEndObject();
    }
}