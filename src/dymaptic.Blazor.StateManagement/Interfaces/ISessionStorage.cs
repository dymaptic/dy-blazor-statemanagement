namespace dymaptic.Blazor.StateManagement.Interfaces;

public interface ISessionStorage
{
    ValueTask<T?> GetItem<T>(string key, CancellationToken cancellationToken = default);
    ValueTask SetItem<T>(string key, T value, CancellationToken cancellationToken = default);
    ValueTask RemoveItem(string key, CancellationToken cancellationToken = default);
}