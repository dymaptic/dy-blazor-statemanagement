using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace dymaptic.Blazor.StateManagement.Server;

public class ServerStateManager<T>(StateManagementDbContext dbContext): IStateManager<T> where T : StateRecord
{
    public async Task<T> Load(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<T>().FindAsync([id], cancellationToken) 
               ?? throw new InvalidOperationException($"State record with ID {id} not found.");
    }
    
    public Task<T> Search(Dictionary<string, string> queryParams, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = dbContext.Set<T>();

        foreach (var param in queryParams)
        {
            query = query.Where(e => EF.Property<string>(e, param.Key).ToString() == param.Value);
        }

        return query.FirstOrDefaultAsync(cancellationToken)
            .ContinueWith(task => task.Result ?? throw new InvalidOperationException("No matching record found."), cancellationToken);
    }

    public Task<T> Save(T model, CancellationToken cancellationToken = default)
    {
        dbContext.Set<T>().Add(model);
        return dbContext.SaveChangesAsync(cancellationToken)
            .ContinueWith(_ => model, cancellationToken);
    }

    public Task<T> Update(T model, CancellationToken cancellationToken = default)
    {
        dbContext.Set<T>().Update(model);
        return dbContext.SaveChangesAsync(cancellationToken)
            .ContinueWith(_ => model, cancellationToken);
    }

    public Task Delete(Guid id, CancellationToken cancellationToken = default)
    {
        T? entity = dbContext.Set<T>().Find(id);
        if (entity is null)
        {
            throw new InvalidOperationException($"State record with ID {id} not found.");
        }

        dbContext.Set<T>().Remove(entity);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<List<T>> LoadAll(Dictionary<string, string>? queryParams, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = dbContext.Set<T>();

        if (queryParams is not null)
        {
            foreach (var param in queryParams)
            {
                query = query.Where(e => EF.Property<string>(e, param.Key).ToString() == param.Value);
            }
        }

        return query.ToListAsync(cancellationToken);
    }

    public Task<List<T>> SaveAll(List<T> models, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Type ModelType => typeof(T);
}