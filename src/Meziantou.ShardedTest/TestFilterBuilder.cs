using System.Text;

namespace Meziantou.ShardedTest;

internal static class TestFilterBuilder
{
    private const string ExactOperator = "FullyQualifiedName=";
    private const string PrefixOperator = "FullyQualifiedName~";
    private const char Separator = '|';

    public static IReadOnlyList<string> BuildFilters(
        IReadOnlyList<string> allTests,
        IReadOnlyList<string> selectedTests,
        int maxFilterLength)
    {
        if (selectedTests.Count == 0)
        {
            return [];
        }

        var allDistinct = allTests.Distinct(StringComparer.Ordinal).ToArray();
        var selectedSet = new HashSet<string>(selectedTests, StringComparer.Ordinal);
        selectedSet.IntersectWith(allDistinct);

        var root = new TestNode(string.Empty);
        foreach (var test in allDistinct)
        {
            root.AddTest(test);
        }

        ComputeCounts(root, selectedSet);

        var parts = new List<string>();
        var safePrefixCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        CollectParts(root, string.Empty, allDistinct, selectedSet, safePrefixCache, parts);

        return SplitFilters(parts, Math.Max(1, maxFilterLength));
    }

    private static void CollectParts(
        TestNode node,
        string prefix,
        IReadOnlyList<string> allTests,
        HashSet<string> selectedSet,
        Dictionary<string, bool> safePrefixCache,
        List<string> parts)
    {
        if (node.SelectedCount == 0)
        {
            return;
        }

        if (node.Children.Count > 0 && node.SelectedCount == node.TotalCount)
        {
            var prefixWithDot = prefix.Length == 0 ? string.Empty : prefix + ".";
            if (prefixWithDot.Length > 0
                && node.SelectedCount > 1
                && IsPrefixShorter(node, prefixWithDot)
                && IsPrefixSafe(prefixWithDot, allTests, selectedSet, safePrefixCache))
            {
                parts.Add(PrefixOperator + prefixWithDot);
                return;
            }
        }

        if (node.Children.Count == 0)
        {
            if (node.FullName is not null && selectedSet.Contains(node.FullName))
            {
                parts.Add(ExactOperator + node.FullName);
            }

            return;
        }

        foreach (var child in node.Children.Values.OrderBy(child => child.Segment, StringComparer.Ordinal))
        {
            var childPrefix = prefix.Length == 0 ? child.Segment : prefix + "." + child.Segment;
            CollectParts(child, childPrefix, allTests, selectedSet, safePrefixCache, parts);
        }
    }

    private static bool IsPrefixShorter(TestNode node, string prefixWithDot)
    {
        if (node.SelectedCount == 0)
        {
            return false;
        }

        var explicitCost = node.ExplicitLeafCost + (node.SelectedCount - 1);
        var prefixCost = PrefixOperator.Length + prefixWithDot.Length;
        return prefixCost < explicitCost;
    }

    private static bool IsPrefixSafe(
        string prefixWithDot,
        IReadOnlyList<string> allTests,
        HashSet<string> selectedSet,
        Dictionary<string, bool> safePrefixCache)
    {
        if (safePrefixCache.TryGetValue(prefixWithDot, out var cached))
        {
            return cached;
        }

        foreach (var test in allTests)
        {
            if (test.Contains(prefixWithDot, StringComparison.Ordinal)
                && !selectedSet.Contains(test))
            {
                safePrefixCache[prefixWithDot] = false;
                return false;
            }
        }

        safePrefixCache[prefixWithDot] = true;
        return true;
    }

    private static List<string> SplitFilters(List<string> parts, int maxFilterLength)
    {
        if (parts.Count == 0)
            return [];

        var filters = new List<string>();
        var builder = new StringBuilder();

        foreach (var part in parts)
        {
            var additionalLength = part.Length + (builder.Length == 0 ? 0 : 1);
            if (builder.Length > 0 && builder.Length + additionalLength > maxFilterLength)
            {
                filters.Add(builder.ToString());
                builder.Clear();
            }

            if (builder.Length == 0 && part.Length > maxFilterLength)
            {
                filters.Add(part);
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(Separator);
            }

            builder.Append(part);
        }

        if (builder.Length > 0)
        {
            filters.Add(builder.ToString());
        }

        return filters;
    }

    private static void ComputeCounts(TestNode node, HashSet<string> selectedSet)
    {
        if (node.Children.Count == 0)
        {
            if (node.FullName is null)
            {
                node.TotalCount = 0;
                node.SelectedCount = 0;
                node.ExplicitLeafCost = 0;
                return;
            }

            node.TotalCount = 1;
            if (selectedSet.Contains(node.FullName))
            {
                node.SelectedCount = 1;
                node.ExplicitLeafCost = ExactOperator.Length + node.FullName.Length;
            }

            return;
        }

        var total = 0;
        var selected = 0;
        var cost = 0;

        foreach (var child in node.Children.Values)
        {
            ComputeCounts(child, selectedSet);
            total += child.TotalCount;
            selected += child.SelectedCount;
            cost += child.ExplicitLeafCost;
        }

        node.TotalCount = total;
        node.SelectedCount = selected;
        node.ExplicitLeafCost = cost;
    }

    private sealed class TestNode
    {
        public TestNode(string segment)
        {
            Segment = segment;
        }

        public string Segment { get; }

        public Dictionary<string, TestNode> Children { get; } = new(StringComparer.Ordinal);

        public string? FullName { get; private set; }

        public int TotalCount { get; set; }

        public int SelectedCount { get; set; }

        public int ExplicitLeafCost { get; set; }

        public void AddTest(string fullName)
        {
            var segments = fullName.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var node = this;

            foreach (var segment in segments)
            {
                if (!node.Children.TryGetValue(segment, out var child))
                {
                    child = new TestNode(segment);
                    node.Children.Add(segment, child);
                }

                node = child;
            }

            node.FullName = fullName;
        }
    }
}
