namespace dymaptic.Blazor.StateManagement.Interfaces;

public interface IStateManager
{
    Type ModelType { get; }
}

public interface IStateManager<T>: IStateManager where T : StateRecord
{
    Task<T> Load(Guid id, CancellationToken cancellationToken = default);
    Task<T> Save(T model, CancellationToken cancellationToken = default);
    Task<T> Update(T model, CancellationToken cancellationToken = default);
    Task Delete(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> LoadAll(CancellationToken cancellationToken = default);
}