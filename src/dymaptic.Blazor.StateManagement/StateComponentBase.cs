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
    public required AuthenticationStateProvider AuthenticationStateProvider { get; set; }
    [Inject]
    public required ILogger<StateComponentBase<T>> Logger { get; set; }

    [Inject]
    public required IStateManager<T> StateManager { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
    [Inject]
    public required TimeProvider TimeProvider { get; set; }

    public T? Model { get; protected set; }
    
    protected virtual bool LoadOnInitialize { get; set; }

    protected virtual async Task Update(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }

        ErrorMessage = null;

        if (Model is null)
        {
            ErrorMessage = "Model is null";

            return;
        }

        try
        {
            Model = await StateManager.Update(Model, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update model");
            ErrorMessage = "Failed to update model";
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
    protected virtual async Task New(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }
        ErrorMessage = null;
        
        if (!StateManager.IsInitialized)
        {
            await StateManager.Initialize(UserId!);
        }
        
        try
        {
            Model = await StateManager.New(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create new model");
            ErrorMessage = "Failed to create new model";
        }
    }

    protected virtual async Task Load(Guid? id = null, CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }
        ErrorMessage = null;
        
        if (!StateManager.IsInitialized)
        {
            await StateManager.Initialize(UserId!);
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
                ErrorMessage = "No ID provided for loading the record";

                return;
            }
        }
        
        ErrorMessage = null;

        try
        {
            Model = await StateManager.Load(id.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load model with ID {Id}", id);
            ErrorMessage = $"Failed to load model with ID {id}";
            Model = null;
        }
    }
    
    protected virtual async Task Search(Dictionary<string, string> queryParams, 
        CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }
        ErrorMessage = null;

        if (!StateManager.IsInitialized)
        {
            await StateManager.Initialize(UserId!);
        }
        
        try
        {
            Model = await StateManager.Search(queryParams, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to search for model with query parameters {QueryParams}", queryParams);
            ErrorMessage = $"Failed to search for model: {ex.Message}";
            Model = null;
        }
    }

    protected virtual async Task Save(CancellationToken cancellationToken = default)
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
            await StateManager.Save(Model, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save model");
            ErrorMessage = $"Failed to save model: {ex.Message}";
        }
    }

    protected virtual async Task Delete(CancellationToken cancellationToken = default)
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
            await StateManager.Delete(Model.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete model with ID {Id}", Model.Id);
            ErrorMessage = $"Failed to delete model with ID {Model.Id}: {ex.Message}";
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
    protected async Task Undo(CancellationToken cancellationToken = default)
    {
        if (!Authenticated)
        {
            ErrorMessage = "User not authenticated";
            return;
        }
        ErrorMessage = null;

        Model = await StateManager.Undo(cancellationToken);
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

        Model = await StateManager.Redo(cancellationToken);
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
            try
            {
                Logger.LogInformation("Tracking model with ID {Id}", Model.Id);
                await StateManager.Track(Model);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to track model with ID {Id}", Model.Id);
                ErrorMessage = $"Failed to track model with ID {Model.Id}: {ex.Message}";
            }
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
    protected string? ErrorMessage;
}