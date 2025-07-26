using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.JSInterop;


namespace dymaptic.Blazor.StateManagement;

public class SessionStorage(IJSRuntime jsRuntime): ISessionStorage
{
    public async ValueTask<T?> GetItem<T>(string key, CancellationToken cancellationToken = default)
    {
        return await jsRuntime.InvokeAsync<T?>("sessionStorage.getItem", cancellationToken, key);
    }

    public async ValueTask SetItem<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", cancellationToken, key, value);
    }

    public ValueTask RemoveItem(string key, CancellationToken cancellationToken = default)
    {
        return jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", cancellationToken, key);
    }
}