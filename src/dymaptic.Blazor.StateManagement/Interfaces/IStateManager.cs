namespace dymaptic.Blazor.StateManagement.Interfaces;

public interface IStateManager
{
    Type ModelType { get; }
}

public interface IStateManager<T>: IStateManager where T : StateRecord
{
    void Initialize(string userId);
    
    ValueTask<T> New(CancellationToken cancellationToken = default);
    ValueTask<T> Load(Guid id, CancellationToken cancellationToken = default);
    ValueTask<T> Track(T model, CancellationToken cancellationToken = default);
    ValueTask<T> Save(T model, CancellationToken cancellationToken = default);
    ValueTask<T> Update(T model, CancellationToken cancellationToken = default);
    ValueTask Delete(Guid id, CancellationToken cancellationToken = default);
    ValueTask<List<T>> LoadAll(Dictionary<string, string>? queryParams,
        CancellationToken cancellationToken = default);
    ValueTask<T?> Search(Dictionary<string, string> queryParams, 
        CancellationToken cancellationToken = default);

    ValueTask<T?> Undo(CancellationToken cancellationToken = default);
    ValueTask<T?> Redo(CancellationToken cancellationToken = default);

    ValueTask<List<T>> SaveAll(List<T> models, CancellationToken cancellationToken = default);
    ValueTask<T?> GetMostRecent(string userId, CancellationToken cancellationToken = default);
    bool IsInitialized { get; }
}