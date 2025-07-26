using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;


namespace dymaptic.Blazor.StateManagement;

public abstract class StateComponentBase<T> : ComponentBase where T : StateRecord, new()
{
    [Inject]
    public required ISessionStorage SessionStorage { get; set; }
    [Inject]
    public required TimeProvider TimeProvider { get; set; }
    [Inject]
    public required AuthenticationStateProvider AuthenticationStateProvider { get; set; }
    [Inject]
    public required ILogger<StateComponentBase<T>> Logger { get; set; }

    [Inject]
    public required IStateManager<T> StateManager { get; set; }

    public T? Model { get; protected set; }
    protected string ItemCacheKey => $"{TypeName}-{Model?.Id}";
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

            PreviousState = Model with { };

            DateTime time = TimeProvider.GetUtcNow().DateTime;

            T snapShot = Model with { LastUpdatedUtc = time };
            UndoStack.Push(snapShot);
            await SessionStorage.SetItem(ItemCacheKey, snapShot, cancellationToken: cancellationToken);
            await SessionStorage.SetItem(LastSavedObjectOfTypeKey, snapShot, cancellationToken: cancellationToken);

            return new StateResult(true, "Changes tracked");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to track changes");

            return new StateResult(false, ex.Message);
        }
    }

    /// <summary>
    ///     Creates a new record of type <typeparamref name="T" />
    /// </summary>
    /// <param name="cancellationToken">
    ///     Optional cancellation token
    /// </param>
    /// <returns>
    ///     A <see cref="StateResult" /> indicating success or failure
    /// </returns>
    public virtual async ValueTask<StateResult> New(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }

        T? backup = Model is null ? null : Model with { };

        try
        {
            DateTime time = TimeProvider.GetUtcNow().DateTime;
            var newModel = new T { Id = Guid.NewGuid(), LastUpdatedUtc = time, CreatedUtc = time };
            Model = newModel;
            UndoStack.Clear();
            RedoStack.Clear();

            await SessionStorage.SetItem(ItemCacheKey, Model, cancellationToken: cancellationToken);
            await SessionStorage.SetItem(LastSavedObjectOfTypeKey, Model, cancellationToken: cancellationToken);

            return new StateResult(true, "New model created");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create new model");
            Model = backup;

            return new StateResult(false, ex.Message);
        }
    }

    public virtual async ValueTask<StateResult> Load(Guid id, bool cacheFirst,
        CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }

        if (cacheFirst && await LoadFromCache(id, cancellationToken))
        {
            return new StateResult(true, "Loaded from cache");
        }

        try
        {
            T record = await StateManager.Load(id, cancellationToken);

            UndoStack.Clear();
            RedoStack.Clear();
            Model = record;
            await SessionStorage.SetItem(ItemCacheKey, Model, cancellationToken: cancellationToken);
            await SessionStorage.SetItem(LastSavedObjectOfTypeKey, Model, cancellationToken: cancellationToken);

            return new StateResult(true, "Loaded from server");
        }
        catch (Exception ex)
        {
            return new StateResult(false, ex.Message);
        }
    }

    public virtual async ValueTask<StateResult> Save(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }

        if (Model is null)
        {
            throw new ArgumentNullException(nameof(Model));
        }

        try
        {
            await StateManager.Save(Model, cancellationToken);

            return new StateResult(true, "Saved to server");
        }
        catch (Exception ex)
        {
            return new StateResult(false, ex.Message);
        }
    }

    public virtual async ValueTask<StateResult> Delete(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }

        if (Model is null)
        {
            throw new ArgumentNullException(nameof(Model));
        }

        try
        {
            await StateManager.Delete(Model.Id, cancellationToken);
            UndoStack.Clear();
            RedoStack.Clear();
            Model = null;

            return new StateResult(true, "Deleted from server");
        }
        catch (Exception ex)
        {
            return new StateResult(false, ex.Message);
        }
    }

    /// <summary>
    ///     Undoes the last change made to the record
    /// </summary>
    /// <param name="cancellationToken">
    ///     Optional cancellation token
    /// </param>
    /// <returns>
    ///     A <see cref="StateResult" /> indicating success or failure
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when the <see cref="Model" /> is null
    /// </exception>
    public async ValueTask<StateResult> Undo(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }

        if (Model is null) throw new ArgumentNullException(nameof(Model));

        T backup = Model with { };

        try
        {
            RedoStack.Push(Model with { });
            Model = UndoStack.Pop();
            await SessionStorage.SetItem(ItemCacheKey, Model, cancellationToken: cancellationToken);
            await SessionStorage.SetItem(LastSavedObjectOfTypeKey, Model, cancellationToken: cancellationToken);

            return new StateResult(true, "Changes tracked");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to undo changes");
            Model = backup;

            return new StateResult(false, ex.Message);
        }
    }

    /// <summary>
    ///     Reapplies the most recent change that was undone
    /// </summary>
    /// <param name="cancellationToken">
    ///     A token to cancel the operation
    /// </param>
    /// <returns>
    ///     A <see cref="StateResult" /> indicating the success of the operation
    /// </returns>
    public virtual async ValueTask<StateResult> Redo(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }

        if (Model is null) throw new ArgumentNullException(nameof(Model));

        T backup = Model with { };

        try
        {
            UndoStack.Push(Model with { });
            Model = RedoStack.Pop();
            await SessionStorage.SetItem(ItemCacheKey, Model, cancellationToken: cancellationToken);
            await SessionStorage.SetItem(LastSavedObjectOfTypeKey, Model, cancellationToken: cancellationToken);

            return new StateResult(true, "Changes tracked");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to redo changes");
            Model = backup;

            return new StateResult(false, ex.Message);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        UserId ??= (await AuthenticationStateProvider.GetAuthenticationStateAsync())
            .User.FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;
        Authenticated = UserId is not null;

        if (!Authenticated)
        {
            return;
        }

        // Restores the last saved object of type <typeparamref name="T" /> from the cache.
        Model = await SessionStorage.GetItem<T?>(LastSavedObjectOfTypeKey);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        await Update();
    }

    protected async Task<bool> LoadFromCache(Guid id, CancellationToken cancellationToken)
    {
        if (!Authenticated)
        {
            return false;
        }

        var key = $"{TypeName}-{id}";

        T? cachedRecord = await SessionStorage.GetItem<T?>(key, cancellationToken);

        if (cachedRecord is not null)
        {
            Model = cachedRecord;

            return true;
        }

        return false;
    }

    protected readonly string TypeName = typeof(T).Name;

    protected readonly Stack<T> RedoStack = [];
    protected readonly Stack<T> UndoStack = [];
    protected bool Authenticated;
    protected T? PreviousState;
    protected string? UserId;
}