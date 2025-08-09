namespace dymaptic.Blazor.StateManagement;

public record SearchRecord(string PropertyName, string SearchValue,
    SearchOption SearchOption, string? SecondarySearchValue = null);

public enum SearchOption
{
    Contains,
    StartsWith,
    EndsWith,
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    In,
    NotIn,
    IsNull,
    IsNotNull,
    Between,
    NotBetween,
    Regex
}