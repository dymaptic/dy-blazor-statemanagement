namespace dymaptic.Blazor.StateManagement;

public static class Extensions
{
    public static string ToLowerFirstChar(this string val)
    {
        return string.Create(val.Length, val, (span, txt) =>
        {
            span[0] = char.ToLower(txt[0]);

            for (var i = 1; i < txt.Length; i++)
            {
                span[i] = txt[i];
            }
        });
    }
    
    public static string GetIndexedDbStoreName(this Type type)
    {
        string storeName = type.Name;

        if (type.IsGenericType)
        {
            // get the generic type definition name
            string argumentName = type.GetGenericArguments()[0].Name;
            storeName = storeName.Replace("`1", $"<{argumentName}>");
        }
        
        return storeName;
    }

    public static bool IsEquatable(this Type type)
    {
        return type.GetInterfaces()
            .Any(i => i.IsGenericType
                && i.GetGenericTypeDefinition() == _iEquatableType);
    }

    public static string BuildQueryString(this List<SearchRecord> searchRecords)
    {
        return string.Join("&", searchRecords.Select(r => 
            $"{Uri.EscapeDataString(r.PropertyName)}={Uri.EscapeDataString(r.SearchValue)}_{Uri.EscapeDataString(r.SearchOption.ToString())}{(r.SecondarySearchValue is null ? "" : $"_{Uri.EscapeDataString(r.SecondarySearchValue)}")}"));
    }
    
    public static List<SearchRecord>? ParseQueryString(this string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return query.Split('&')
            .Select(part => part.Split('='))
            .Where(parts => parts.Length == 2)
            .Select(p =>
            {
                string propertyName = Uri.UnescapeDataString(p[0]);
                string[] values = Uri.UnescapeDataString(p[1]).Split('_');

                return values.Length switch
                {
                    < 2 => null,
                    2 => new SearchRecord(propertyName, values[0], 
                        Enum.Parse<SearchOption>(values[1])),
                    _ => new SearchRecord(propertyName,values[0],
                        Enum.Parse<SearchOption>(values[1]), 
                        values[2])  
                };
            })
            .Where(parts => parts is not null)
            .Cast<SearchRecord>()
            .ToList();
    }

    private static Type _iEquatableType = typeof(IEquatable<>);
}