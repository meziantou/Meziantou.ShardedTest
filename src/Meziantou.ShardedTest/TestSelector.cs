namespace Meziantou.ShardedTest;

internal static class TestSelector
{
    public static IReadOnlyList<string> SelectTests(IEnumerable<string> tests, int shardIndex, int totalShards)
    {
        var ordered = tests.Order(StringComparer.Ordinal).ToArray();
        var selected = new List<string>();
        var targetRemainder = shardIndex - 1;

        for (var i = 0; i < ordered.Length; i++)
        {
            if (i % totalShards == targetRemainder)
            {
                selected.Add(ordered[i]);
            }
        }

        return selected;
    }
}
