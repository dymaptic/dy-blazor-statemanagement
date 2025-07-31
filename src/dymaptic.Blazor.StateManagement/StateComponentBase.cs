using System.Collections.Specialized;
using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Web;


namespace dymaptic.Blazor.StateManagement;

public abstract class StateComponentBase<T> : ComponentBase where T : StateRecord, new()
{
    [Inject]
    public required IndexedDb IndexedDb { get; set; }
    [Inject]
    public required TimeProvider TimeProvider { get; set; }
    [Inject]
    public required AuthenticationStateProvider AuthenticationStateProvider { get; set; }
    [Inject]
    public required ILogger<StateComponentBase<T>> Logger { get; set; }

    [Inject]
    public required IStateManager<T> StateManager { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    public T? Model { get; protected set; }
    
    protected virtual bool LoadOnInitialize { get; set; }
    protected virtual TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

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
            await SaveToIndexedDb(snapShot, cancellationToken);
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

            await SaveToIndexedDb(newModel, cancellationToken);

            if (backup is not null)
            {
                // delete the previous item from the IndexedDb cache
                await IndexedDb.Delete<T>(backup.Id, cancellationToken);
            }

            return new StateResult(true, "New model created");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create new model");
            Model = backup;

            return new StateResult(false, ex.Message);
        }
    }

    public virtual async ValueTask<StateResult> Load(Guid? id = null, CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }
        
        // get ID from the URL if it exists
        if (id is null)
        {
            Dictionary<string, string>? queryParams = ParseQueryString(QueryString);
                    
            if (queryParams?.TryGetValue("id", out var idParam) == true)
            {
                id = Guid.TryParse(idParam, out Guid parsedId) ? parsedId : null;
            }
            else
            {
                // check if the ID is in the URL path
                Uri uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
                string[] segments = uri.Segments;
                if (segments.Length > 1 && Guid.TryParse(segments[^1].TrimEnd('/'), out Guid parsedId))
                {
                    id = parsedId;
                }
            }
            
            if (id is null)
            {
                return new StateResult(false, "No ID provided for loading the record");
            }
        }
        
        T? backup = Model;

        try
        {
            T? cachedRecord = await LoadFromIndexedDb(id.Value, cancellationToken);
        
            if (cachedRecord is not null)
            {
                Model = cachedRecord;
                UndoStack.Clear();
                RedoStack.Clear();
                return new StateResult(true, "Loaded from IndexedDb cache");
            }
            
            T record = await StateManager.Load(id.Value, cancellationToken);

            UndoStack.Clear();
            RedoStack.Clear();
            Model = record;
            await SaveToIndexedDb(Model, cancellationToken);

            return new StateResult(true, "Loaded from server");
        }
        catch (Exception ex)
        {
            Model = backup;
            return new StateResult(false, ex.Message);
        }
    }
    
    public virtual async ValueTask<StateResult> Search(Dictionary<string, string> queryParams, 
        CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }

        try
        {
            T? cachedRecord = await LoadFromIndexedDb(Guid.Empty, cancellationToken);

            if (cachedRecord is not null)
            {
                Model = cachedRecord;
                UndoStack.Clear();
                RedoStack.Clear();
                return new StateResult(true, "Loaded from IndexedDb cache");
            }

            T record = await StateManager.Search(queryParams, cancellationToken);
            Model = record;
            UndoStack.Clear();
            RedoStack.Clear();
            await SaveToIndexedDb(Model, cancellationToken);

            return new StateResult(true, "Loaded from server");
        }
        catch (Exception ex)
        {
            return new StateResult(false, ex.Message);
        }
    }
    
    private Dictionary<string, string>? ParseQueryString(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return null;
        }
        
        NameValueCollection query = HttpUtility.ParseQueryString(queryString);
        Dictionary<string, string> queryParams = new();
        foreach (string? key in query.AllKeys)
        {
            if (key is not null && query[key] is not null)
            {
                queryParams[key] = query[key]!;
            }
        }
        
        return queryParams;
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
            await IndexedDb.Delete<T>(Model.Id, cancellationToken);
            Model = null;

            return new StateResult(true, "Deleted from server and IndexedDb");
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
            await SaveToIndexedDb(Model, cancellationToken);

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
            await SaveToIndexedDb(Model, cancellationToken);

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

        await IndexedDb.Initialize();
        
        Uri uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        QueryString = uri.Query;

        if (LoadOnInitialize && Model is null)
        {
            await Load();
        }
    }
    
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        string? previousQueryString = QueryString;
        Uri uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        QueryString = uri.Query;
        if (previousQueryString is not null && QueryString != previousQueryString && LoadOnInitialize)
        {
            await Load();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        await Update();
    }

    private async Task SaveToIndexedDb(T record, CancellationToken cancellationToken)
    {
        await IndexedDb.Put(new CacheStorageRecord<T>(record, record.Id, UserId!, TimeProvider.GetUtcNow().DateTime), 
            cancellationToken);
    }
    
    private async Task<T?> LoadFromIndexedDb(Guid id, CancellationToken cancellationToken)
    {
        CacheStorageRecord<T>? cachedRecord = await IndexedDb.Get<CacheStorageRecord<T>>(id, cancellationToken);

        if (cachedRecord is not null && cachedRecord.UserId == UserId
            && cachedRecord.TimeStamp + CacheDuration < TimeProvider.GetUtcNow().DateTime)
        {
            return cachedRecord.Item;
        }

        return null;
    }

    protected readonly string TypeName = typeof(T).Name;

    protected readonly Stack<T> RedoStack = [];
    protected readonly Stack<T> UndoStack = [];
    protected bool Authenticated;
    protected T? PreviousState;
    protected string? UserId;
    protected string? QueryString;
}