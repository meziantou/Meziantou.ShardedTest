namespace Meziantou.ShardedTest;

internal static class TestSelector
{
    public static IReadOnlyList<string> SelectTests(IEnumerable<string> tests, int shardIndex, int totalShards)
    {
        var ordered = tests.Order(StringComparer.Ordinal).ToArray();
        return SelectTests(ordered, shardIndex, totalShards);
    }

    public static IReadOnlyList<DiscoveredTest> SelectTests(IEnumerable<DiscoveredTest> tests, int shardIndex, int totalShards)
    {
        var ordered = tests
            .OrderBy(test => test.TargetFramework ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(test => test.Name, StringComparer.Ordinal)
            .ToArray();
        return SelectTests(ordered, shardIndex, totalShards);
    }

    private static List<TTest> SelectTests<TTest>(IReadOnlyList<TTest> ordered, int shardIndex, int totalShards)
    {
        var selected = new List<TTest>();
        var targetRemainder = shardIndex - 1;

        for (var i = 0; i < ordered.Count; i++)
        {
            if (i % totalShards == targetRemainder)
            {
                selected.Add(ordered[i]);
            }
        }

        return selected;
    }
}
