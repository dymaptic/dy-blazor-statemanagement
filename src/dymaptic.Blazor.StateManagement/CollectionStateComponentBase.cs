using System.Collections.Specialized;
using dymaptic.Blazor.StateManagement.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Web;


namespace dymaptic.Blazor.StateManagement;

public abstract class CollectionStateComponentBase<T> : ComponentBase where T : StateRecord, new()
{
    [Inject]
    public required IndexedDb IndexedDb { get; set; }
    [Inject]
    public required TimeProvider TimeProvider { get; set; }
    [Inject]
    public required AuthenticationStateProvider AuthenticationStateProvider { get; set; }
    [Inject]
    public required ILogger<CollectionStateComponentBase<T>> Logger { get; set; }

    [Inject]
    public required IStateManager<T> StateManager { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    public List<T>? Model { get; protected set; }
    
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

            PreviousState = Model.Select(m => m with { }).ToList();

            DateTime time = TimeProvider.GetUtcNow().DateTime;
            List<T> collectionSnapshot = [];

            foreach (T item in Model)
            {
                T snapShot = item with { LastUpdatedUtc = time };
                collectionSnapshot.Add(snapShot);
            }
            
            UndoStack.Push(collectionSnapshot);
            await SaveToIndexedDb(collectionSnapshot, cancellationToken);

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

            await SaveToIndexedDb(Model, cancellationToken);

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
            await SaveToIndexedDb(Model, cancellationToken);

            return new StateResult(true, "Item removed");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to remove item");
            Model = UndoStack.Pop();

            return new StateResult(false, ex.Message);
        }
    }
    
    public virtual async ValueTask<StateResult> Clear(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }

        if (Model is null || !Model.Any())
        {
            return new StateResult(false, "No items to clear");
        }

        List<T> backup = Model.Select(m => m with { }).ToList();
        UndoStack.Push(backup);

        try
        {
            Model.Clear();
            await SaveToIndexedDb(Model, cancellationToken);

            return new StateResult(true, "All items cleared");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to clear items");
            Model = UndoStack.Pop();

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
            Model = await StateManager.SaveAll(Model, cancellationToken);
            UndoStack.Clear();
            RedoStack.Clear();
            await SaveToIndexedDb(Model, cancellationToken);
    
            return new StateResult(true, "Saved to server");
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
    
        List<T> backup = Model.Select(m => m with { }).ToList();
    
        try
        {
            RedoStack.Push(backup);
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
    
        List<T> backup = Model.Select(m => m with { }).ToList();
    
        try
        {
            UndoStack.Push(backup);
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

        if (Model is null && LoadOnInitialize)
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

    protected virtual async Task<StateResult> Load(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            return new StateResult(false, "User not authenticated");
        }
        
        List<T>? backup = Model?.Select(m => m with { }).ToList();
        
        try
        {
            List<T>? cachedItems = await LoadFromIndexedDb(QueryString, cancellationToken);
            
            if (cachedItems is not null)
            {
                Model = cachedItems;
                UndoStack.Clear();
                RedoStack.Clear();
                return new StateResult(true, "Loaded from cache");
            }
            
            Dictionary<string, string>? queryParams = ParseQueryString(QueryString);
            
            Model = await StateManager.LoadAll(queryParams, cancellationToken);
        
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
            return QueryParams;
        }
        NameValueCollection query = HttpUtility.ParseQueryString(queryString);
        QueryParams = new Dictionary<string, string>();
        foreach (string? key in query.AllKeys)
        {
            if (key is not null && query[key] is not null)
            {
                QueryParams[key] = query[key]!;
            }
        }
        
        return QueryParams;
    }
    
    private async Task SaveToIndexedDb(List<T> record, CancellationToken cancellationToken)
    {
        await IndexedDb.Put(new CacheStorageCollectionRecord<T>(record, $"{UserId}-{QueryString}", 
            QueryString ?? string.Empty, UserId!, TimeProvider.GetUtcNow().DateTime), cancellationToken);
    }
    
    private async Task<List<T>?> LoadFromIndexedDb(string? queryString, CancellationToken cancellationToken)
    {
        CacheStorageCollectionRecord<T>? cachedRecord = 
            await IndexedDb.Get<CacheStorageCollectionRecord<T>>($"{UserId}-{queryString}", cancellationToken);

        if (cachedRecord is not null && cachedRecord.UserId == UserId
            && cachedRecord.TimeStamp + CacheDuration < TimeProvider.GetUtcNow().DateTime)
        {
            return cachedRecord.Items;
        }

        return null;
    }

    protected readonly string TypeName = typeof(T).Name;

    protected readonly Stack<List<T>> RedoStack = [];
    protected readonly Stack<List<T>> UndoStack = [];
    protected bool Authenticated;
    protected List<T>? PreviousState;
    protected string? UserId;
    protected string? QueryString;
    protected Dictionary<string, string>? QueryParams;
}