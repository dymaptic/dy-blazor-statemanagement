using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;


namespace dymaptic.Blazor.StateManagement;

public abstract class CollectionStateComponentBase<T> : ComponentBase where T : StateRecord, new()
{
    [Inject]
    public required ISessionStorage SessionStorage { get; set; }
    [Inject]
    public required TimeProvider TimeProvider { get; set; }
    [Inject]
    public required AuthenticationStateProvider AuthenticationStateProvider { get; set; }
    [Inject]
    public required ILogger<CollectionStateComponentBase<T>> Logger { get; set; }

    [Inject]
    public required IStateManager<T> StateManager { get; set; }

    public List<T>? Model { get; protected set; }
    
    protected string ItemCacheKey(T item) => $"{TypeName}-{item.Id}";
    protected string LastSavedObjectOfTypeKey => $"{UserId}-{TypeName}-last-saved";

    public virtual async ValueTask<StateResult> Update(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }

        if (Model is null) return new StateResult(false, "No model to track");

        try
        {
            if (Model == PreviousState)
            {
                return new StateResult(true, "No Changes");
            }

            PreviousState = Model.Select(m => m with { }).ToList();

            DateTime time = TimeProvider.GetUtcNow().DateTime;
            List<T> collectionSnapshot = [];

            foreach (T item in Model)
            {
                T snapShot = item with { LastUpdatedUtc = time };
                collectionSnapshot.Add(snapShot);
                await SessionStorage.SetItem(ItemCacheKey(item), snapShot, cancellationToken);
            }
            
            UndoStack.Push(collectionSnapshot);
            await SessionStorage.SetItem(LastSavedObjectOfTypeKey, collectionSnapshot, 
                cancellationToken: cancellationToken);

            return new StateResult(true, "Changes tracked");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to track changes");

            return new StateResult(false, ex.Message);
        }
    }

    /// <summary>
    ///     Adds a record of type <typeparamref name="T" />
    /// </summary>
    /// <param name="cancellationToken">
    ///     Optional cancellation token
    /// </param>
    /// <returns>
    ///     A <see cref="StateResult" /> indicating success or failure
    /// </returns>
    public virtual async ValueTask<StateResult> Add(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }
        
        Model ??= [];

        List<T> backup = Model.Select(m => m with { }).ToList();
        UndoStack.Push(backup);

        try
        {
            DateTime time = TimeProvider.GetUtcNow().DateTime;
            var newItem = new T { Id = Guid.NewGuid(), LastUpdatedUtc = time, CreatedUtc = time };
            Model ??= [];
            Model.Add(newItem);

            await SessionStorage.SetItem(ItemCacheKey(newItem), newItem, cancellationToken: cancellationToken);
            await SessionStorage.SetItem(LastSavedObjectOfTypeKey, Model, cancellationToken: cancellationToken);

            return new StateResult(true, "New item added");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add new item");
            Model = UndoStack.Pop();

            return new StateResult(false, ex.Message);
        }
    }

    public virtual async ValueTask<StateResult> Remove(T item, CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }

        if (Model is null || !Model.Contains(item))
        {
            return new StateResult(false, "Item not found in model");
        }

        List<T> backup = Model.Select(m => m with { }).ToList();
        UndoStack.Push(backup);

        try
        {
            Model.Remove(item);
            await SessionStorage.SetItem(LastSavedObjectOfTypeKey, Model, cancellationToken: cancellationToken);
            await SessionStorage.SetItem(ItemCacheKey(item), Model, cancellationToken: cancellationToken);

            return new StateResult(true, "Item removed");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to remove item");
            Model = UndoStack.Pop();

            return new StateResult(false, ex.Message);
        }
    }

    // public virtual async ValueTask<StateResult> Load(Guid id, bool cacheFirst,
    //     CancellationToken cancellationToken = default)
    // {
    //     if (!Authenticated)
    //     {
    //         return new StateResult(false, "User not authenticated");
    //     }
    //
    //     if (cacheFirst && await LoadFromCache(id, cancellationToken))
    //     {
    //         return new StateResult(true, "Loaded from cache");
    //     }
    //
    //     try
    //     {
    //         T record = await StateManager.Load(id, cancellationToken);
    //
    //         UndoStack.Clear();
    //         RedoStack.Clear();
    //         Model = record;
    //         await Cache.SetAsync(ItemCacheKey, Model, cancellationToken: cancellationToken);
    //         await Cache.SetAsync(LastSavedObjectOfTypeKey, Model, cancellationToken: cancellationToken);
    //
    //         return new StateResult(true, "Loaded from server");
    //     }
    //     catch (Exception ex)
    //     {
    //         return new StateResult(false, ex.Message);
    //     }
    // }
    //
    // public virtual async ValueTask<StateResult> Save(CancellationToken cancellationToken = default)
    // {
    //     if (!Authenticated)
    //     {
    //         return new StateResult(false, "User not authenticated");
    //     }
    //
    //     if (Model is null)
    //     {
    //         throw new ArgumentNullException(nameof(Model));
    //     }
    //
    //     try
    //     {
    //         await StateManager.Save(Model, cancellationToken);
    //
    //         return new StateResult(true, "Saved to server");
    //     }
    //     catch (Exception ex)
    //     {
    //         return new StateResult(false, ex.Message);
    //     }
    // }
    //
    // public virtual async ValueTask<StateResult> Delete(CancellationToken cancellationToken = default)
    // {
    //     if (!Authenticated)
    //     {
    //         return new StateResult(false, "User not authenticated");
    //     }
    //
    //     if (Model is null)
    //     {
    //         throw new ArgumentNullException(nameof(Model));
    //     }
    //
    //     try
    //     {
    //         await StateManager.Delete(Model.Id, cancellationToken);
    //         UndoStack.Clear();
    //         RedoStack.Clear();
    //         Model = null;
    //
    //         return new StateResult(true, "Deleted from server");
    //     }
    //     catch (Exception ex)
    //     {
    //         return new StateResult(false, ex.Message);
    //     }
    // }
    //
    // /// <summary>
    // ///     Undoes the last change made to the record
    // /// </summary>
    // /// <param name="cancellationToken">
    // ///     Optional cancellation token
    // /// </param>
    // /// <returns>
    // ///     A <see cref="StateResult" /> indicating success or failure
    // /// </returns>
    // /// <exception cref="ArgumentNullException">
    // ///     Thrown when the <see cref="Model" /> is null
    // /// </exception>
    // public async ValueTask<StateResult> Undo(CancellationToken cancellationToken = default)
    // {
    //     if (!Authenticated)
    //     {
    //         return new StateResult(false, "User not authenticated");
    //     }
    //
    //     if (Model is null) throw new ArgumentNullException(nameof(Model));
    //
    //     T backup = Model with { };
    //
    //     try
    //     {
    //         RedoStack.Push(Model with { });
    //         Model = UndoStack.Pop();
    //         await Cache.SetAsync(ItemCacheKey, Model, cancellationToken: cancellationToken);
    //         await Cache.SetAsync(LastSavedObjectOfTypeKey, Model, cancellationToken: cancellationToken);
    //
    //         return new StateResult(true, "Changes tracked");
    //     }
    //     catch (Exception ex)
    //     {
    //         Logger.LogError(ex, "Failed to undo changes");
    //         Model = backup;
    //
    //         return new StateResult(false, ex.Message);
    //     }
    // }
    //
    // /// <summary>
    // ///     Reapplies the most recent change that was undone
    // /// </summary>
    // /// <param name="cancellationToken">
    // ///     A token to cancel the operation
    // /// </param>
    // /// <returns>
    // ///     A <see cref="StateResult" /> indicating the success of the operation
    // /// </returns>
    // public virtual async ValueTask<StateResult> Redo(CancellationToken cancellationToken = default)
    // {
    //     if (!Authenticated)
    //     {
    //         return new StateResult(false, "User not authenticated");
    //     }
    //
    //     if (Model is null) throw new ArgumentNullException(nameof(Model));
    //
    //     T backup = Model with { };
    //
    //     try
    //     {
    //         UndoStack.Push(Model with { });
    //         Model = RedoStack.Pop();
    //         await Cache.SetAsync(ItemCacheKey, Model, cancellationToken: cancellationToken);
    //         await Cache.SetAsync(LastSavedObjectOfTypeKey, Model, cancellationToken: cancellationToken);
    //
    //         return new StateResult(true, "Changes tracked");
    //     }
    //     catch (Exception ex)
    //     {
    //         Logger.LogError(ex, "Failed to redo changes");
    //         Model = backup;
    //
    //         return new StateResult(false, ex.Message);
    //     }
    // }
    //
    // protected override async Task OnInitializedAsync()
    // {
    //     UserId ??= (await AuthenticationStateProvider.GetAuthenticationStateAsync())
    //         .User.FindFirst(ClaimTypes.NameIdentifier)
    //         ?.Value;
    //     Authenticated = UserId is not null;
    //
    //     if (!Authenticated)
    //     {
    //         return;
    //     }
    //
    //     await RestoreFromCache();
    // }
    //
    // /// <summary>
    // ///     Restores the last saved object of type <typeparamref name="T" /> from the cache.
    // /// </summary>
    // protected virtual async Task RestoreFromCache()
    // {
    //     Model = await Cache.GetOrCreateAsync<T?>(LastSavedObjectOfTypeKey,
    //         async token =>
    //         {
    //             DateTime time = TimeProvider.GetUtcNow().DateTime;
    //             var model = new T { Id = Guid.NewGuid(), LastUpdatedUtc = time, CreatedUtc = time };
    //             await Cache.SetAsync(ItemCacheKey, Model, cancellationToken: token);
    //
    //             return model;
    //         });
    // }
    //
    // protected async Task<bool> LoadFromCache(Guid id, CancellationToken cancellationToken)
    // {
    //     if (!Authenticated)
    //     {
    //         return false;
    //     }
    //
    //     var key = $"{TypeName}-{id}";
    //
    //     T? cachedRecord = await Cache.GetOrCreateAsync<T?>(key, _ => ValueTask.FromResult(default(T?)),
    //         cancellationToken: cancellationToken);
    //
    //     if (cachedRecord is not null)
    //     {
    //         Model = cachedRecord;
    //
    //         return true;
    //     }
    //
    //     return false;
    // }

    protected readonly string TypeName = typeof(T).Name;

    protected readonly Stack<List<T>> RedoStack = [];
    protected readonly Stack<List<T>> UndoStack = [];
    protected bool Authenticated;
    protected List<T>? PreviousState;
    protected string? UserId;
}