namespace StockViewer.Shared.Money;

public sealed record AllocationItem(string Key, string Label, Currency Currency, decimal? Value);

public sealed record AllocationSlice(string Key, string Label, decimal Value, decimal Percent);

public static class Allocation
{
    public const string OtherKey = "other";

    /// <summary>Groups priced items by key, converts them to <paramref name="target"/> and returns each group's share, largest first.</summary>
    public static IReadOnlyList<AllocationSlice> Slices(
        IEnumerable<AllocationItem> items,
        Currency target,
        decimal usdBrlRate)
    {
        ArgumentNullException.ThrowIfNull(items);

        var groups = new List<(string Key, string Label, decimal Value)>();
        var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item.Value is not { } amount)
            {
                continue;
            }
            var value = FxConversion.Convert(amount, item.Currency, target, usdBrlRate);
            if (indexByKey.TryGetValue(item.Key, out var index))
            {
                groups[index] = groups[index] with { Value = groups[index].Value + value };
            }
            else
            {
                indexByKey[item.Key] = groups.Count;
                groups.Add((item.Key, item.Label, value));
            }
        }

        var total = groups.Sum(group => group.Value);
        if (total == 0)
        {
            return [];
        }
        return groups
            .Select(group => new AllocationSlice(group.Key, group.Label, group.Value, group.Value / total * 100))
            .OrderByDescending(slice => slice.Value)
            .ToList();
    }

    /// <summary>Keeps the largest <c>maxSlices - 1</c> slices and merges the rest into "Other".</summary>
    public static IReadOnlyList<AllocationSlice> GroupOther(IReadOnlyList<AllocationSlice> slices, int maxSlices)
    {
        ArgumentNullException.ThrowIfNull(slices);
        if (slices.Count <= maxSlices)
        {
            return slices;
        }
        var kept = slices.Take(maxSlices - 1).ToList();
        var rest = slices.Skip(maxSlices - 1).ToList();
        var other = new AllocationSlice(
            OtherKey,
            "Other",
            rest.Sum(slice => slice.Value),
            rest.Sum(slice => slice.Percent));
        return [.. kept, other];
    }
}
