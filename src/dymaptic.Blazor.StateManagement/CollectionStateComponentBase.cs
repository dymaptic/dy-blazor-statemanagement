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

    public virtual async Task Update(T item, CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }
        ErrorMessage = null;
        Model ??= [];

        try
        {
            item = await StateManager.Update(item, cancellationToken);
            int index = Model.FindIndex(m => m.Id == item.Id);
            if (index >= 0)
            {
                Model[index] = item;
            }
            else
            {
                Model.Add(item);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update model");
            ErrorMessage = "Failed to update model";
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
    public virtual async Task Add(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }
        ErrorMessage = null;
        
        Model ??= [];

        try
        {
            T item = await StateManager.New(cancellationToken);
            Model.Add(item);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add new item");
            ErrorMessage = $"Failed to add new item: {ex.Message}";
        }
    }

    public virtual async Task Delete(T item, CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }

        if (Model is null || !Model.Contains(item))
        {
            ErrorMessage = "Item not found in model";
            return;
        }
        ErrorMessage = null;

        try
        {
            Model.Remove(item);
            await StateManager.Delete(item.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to remove item");
            ErrorMessage = $"Failed to remove item: {ex.Message}";
            Model.Add(item); // Revert the removal in case of failure
        }
    }
    
    /// <summary>
    ///     Clear does not delete items from the database, but clears the local model.
    /// </summary>
    protected virtual void Clear(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }

        if (Model is null || !Model.Any())
        {
            ErrorMessage = "No items to clear";
            return;
        }
        ErrorMessage = null;

        try
        {
            Model.Clear();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to clear items");
        }
    }
    
    public virtual async Task Save(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }
    
        if (Model is null)
        {
            throw new ArgumentNullException(nameof(Model));
        }
        ErrorMessage = null;
    
        try
        {
            Model = await StateManager.SaveAll(Model, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save model");
            ErrorMessage = $"Failed to save model: {ex.Message}";
        }
    }
    

    protected virtual async Task Load(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }
        ErrorMessage = null;
        
        try
        {
            Dictionary<string, string>? queryParams = ParseQueryString(QueryString);
            Model = await StateManager.LoadAll(queryParams, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load model with query parameters {QueryParams}", QueryString);
            ErrorMessage = $"Failed to load model: {ex.Message}";
            Model = null;
        }
    }
    
    protected async Task Undo(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }
        ErrorMessage = null;

        T? item = await StateManager.Undo(cancellationToken);
        if (item is null)
        {
            ErrorMessage = "No changes to undo";

            return;
        }
        
        T? oldItem = Model?.FirstOrDefault(m => m.Id == item.Id);
        if (oldItem is not null)
        {
            int index = Model!.IndexOf(oldItem);
            Model!.Remove(oldItem);
            Model.Insert(index, item);
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
    public virtual async Task Redo(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }
        ErrorMessage = null;

        T? item = await StateManager.Redo(cancellationToken);
        if (item is null)
        {
            ErrorMessage = "No changes to redo";

            return;
        }

        T? oldItem = Model?.FirstOrDefault(m => m.Id == item.Id);
        if (oldItem is not null)
        {
            int index = Model!.IndexOf(oldItem);
            Model!.Remove(oldItem);
            Model.Insert(index, item);
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
        
        Uri uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        QueryString = uri.Query;
    }
    
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        string? previousQueryString = QueryString;
        Uri uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        QueryString = uri.Query;
        if (previousQueryString is not null 
            && QueryString != previousQueryString 
            && LoadOnInitialize
            && !StateManager.IsInitialized)
        {
            await Load();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            if (LoadOnInitialize && Model is null)
            {
                await Load();
            }
        }

        if (Model is not null)
        {
            foreach (T item in Model)
            {
                try
                {
                    await StateManager.Track(item);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to track item with ID {Id}", item.Id);
                    ErrorMessage = $"Failed to track item with ID {item.Id}";

                    break;
                }
            }
        }
    }

    private Dictionary<string, string>? ParseQueryString(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return QueryParams;
        }
        NameValueCollection query = HttpUtility.ParseQueryString(queryString);
        Dictionary<string, string> queryParams = new();
        foreach (string? key in query.AllKeys)
        {
            if (key is not null && !string.IsNullOrWhiteSpace(key))
            {
                queryParams[key] = query[key]!;
            }
        }
        
        return queryParams;
    }

    protected bool Authenticated;
    protected string? UserId;
    protected string? QueryString;
    protected Dictionary<string, string>? QueryParams;
    protected string? ErrorMessage;
}