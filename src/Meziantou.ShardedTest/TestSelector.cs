namespace Meziantou.ShardedTest;

internal static class TestSelector
{
    public static IReadOnlyList<string> SelectTests(IEnumerable<string> tests, int jobNumber, int totalJobs)
    {
        var ordered = tests.Order(StringComparer.Ordinal).ToArray();
        var selected = new List<string>();
        var targetRemainder = jobNumber - 1;

        for (var i = 0; i < ordered.Length; i++)
        {
            if (i % totalJobs == targetRemainder)
            {
                selected.Add(ordered[i]);
            }
        }

        return selected;
    }
}
