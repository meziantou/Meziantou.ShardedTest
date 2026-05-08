namespace Meziantou.ShardedTest;

internal static class TestListParser
{
    private const string UnknownFramework = "<unknown>";
    private const string ListHeader = "The following Tests are available:";
    private const string TestRunPrefix = "Test run for ";

    public static IReadOnlyList<DiscoveredTest> Parse(string output)
    {
        var lines = output.Split(["\r\n", "\n"], StringSplitOptions.None);
        var tests = new List<DiscoveredTest>();
        var inList = false;
        string? currentTargetFramework = null;

        foreach (var line in lines)
        {
            var framework = ExtractTargetFramework(line);
            if (framework is not null)
            {
                currentTargetFramework = framework;
                inList = false;
                continue;
            }

            if (!inList)
            {
                if (line.Contains(ListHeader, StringComparison.OrdinalIgnoreCase))
                {
                    inList = true;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();

            if (IsEndOfList(trimmed))
            {
                inList = false;
                continue;
            }

            if (TryParseTest(trimmed, currentTargetFramework, out var parsedTest))
            {
                tests.Add(parsedTest);
            }
        }

        return tests;
    }

    private static bool IsEndOfList(string line)
    {
        return line.StartsWith("Total tests:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Test Run", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Build ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Results ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseTest(string line, string? currentTargetFramework, out DiscoveredTest parsedTest)
    {
        if (line.StartsWith('(')
            && line.EndsWith(')')
            && TryParseTuple(line, out var parsed))
        {
            parsedTest = parsed;
            return true;
        }

        if (LooksLikeFullyQualifiedTestName(line))
        {
            parsedTest = new DiscoveredTest(currentTargetFramework, line);
            return true;
        }

        parsedTest = null!;
        return false;
    }

    private static bool LooksLikeFullyQualifiedTestName(string line)
    {
        return line.Contains('.', StringComparison.Ordinal)
            && !line.Contains(' ', StringComparison.Ordinal)
            && !line.Contains(':', StringComparison.Ordinal);
    }

    private static bool TryParseTuple(string line, out DiscoveredTest result)
    {
        var tuple = line[1..^1];
        var separatorIndex = tuple.IndexOf(", ", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex + 2 >= tuple.Length)
        {
            result = null!;
            return false;
        }

        var framework = NormalizeFramework(tuple[..separatorIndex]);
        var name = tuple[(separatorIndex + 2)..];
        result = new DiscoveredTest(framework, name);
        return true;
    }

    private static string? NormalizeFramework(string framework)
    {
        if (framework.Equals(UnknownFramework, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return framework.Length == 0 ? null : framework;
    }

    private static string? ExtractTargetFramework(string line)
    {
        var trimmedLine = line.TrimStart();
        if (!trimmedLine.StartsWith(TestRunPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = trimmedLine[TestRunPrefix.Length..].Trim();
        var detailsIndex = path.IndexOf(" (", StringComparison.Ordinal);
        if (detailsIndex >= 0)
        {
            path = path[..detailsIndex];
        }

        var fileSeparatorIndex = path.LastIndexOfAny(['\\', '/']);
        if (fileSeparatorIndex <= 0)
        {
            return null;
        }

        var directory = path[..fileSeparatorIndex];
        var frameworkSeparatorIndex = directory.LastIndexOfAny(['\\', '/']);
        if (frameworkSeparatorIndex < 0 || frameworkSeparatorIndex + 1 >= directory.Length)
        {
            return null;
        }

        var framework = directory[(frameworkSeparatorIndex + 1)..].Trim();
        return framework.Length == 0 ? null : framework;
    }
}
