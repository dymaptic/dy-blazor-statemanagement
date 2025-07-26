namespace dymaptic.Blazor.StateManagement;

/// <summary>
///     Notifies the caller of success or error messages when performing operations on a RecordManager
/// </summary>
public record StateResult(bool Success, string? Message);

/// <summary>
///     Returns a <see cref="StateRecord"/> or an error message
/// </summary>
public record StateRecordResult<TRecord>(bool Success, TRecord? Record, string? Message = null) 
    where TRecord : StateRecord;

/// <summary>
///     Returns an array of <see cref="StateRecord"/>s or an error message
/// </summary>
public record StateRecordCollectionResult<TRecord>(bool Success, IReadOnlyList<TRecord>? Records, string? Message)
    where TRecord : StateRecord;