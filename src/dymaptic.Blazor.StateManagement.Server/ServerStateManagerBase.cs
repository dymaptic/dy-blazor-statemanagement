using dymaptic.Blazor.StateManagement.Interfaces;


namespace dymaptic.Blazor.StateManagement.Server;

public abstract class ServerStateManagerBase<T> : IStateManager<T> where T : StateRecord
{
    public abstract Task<T> Load(Guid id, CancellationToken cancellationToken = default);

    public abstract Task<T> Save(T model, CancellationToken cancellationToken = default);

    public abstract Task<T> Update(T model, CancellationToken cancellationToken = default);

    public abstract Task Delete(Guid id, CancellationToken cancellationToken = default);

    public abstract Task<IEnumerable<T>> LoadAll(CancellationToken cancellationToken = default);
    
    public Type ModelType => typeof(T);
}