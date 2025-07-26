namespace dymaptic.Blazor.StateManagement;

public record StateRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSavedUtc { get; set; }
    public Guid? CreatorId { get; init; }

    public virtual bool Equals(StateRecord? other)
    {
        return Id.Equals(other?.Id);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }
}