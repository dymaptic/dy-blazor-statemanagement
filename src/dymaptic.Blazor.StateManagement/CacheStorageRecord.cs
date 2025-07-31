namespace dymaptic.Blazor.StateManagement;

public record CacheStorageRecord<T>(T Item, Guid ItemId, string UserId, DateTime TimeStamp) where T: StateRecord;

public record CacheStorageCollectionRecord<T>(List<T> Items, string ListId, string QueryString, 
    string UserId, DateTime TimeStamp) where T: StateRecord;