namespace Meziantou.ShardedTest;

internal static class TestListParser
{
    public static IReadOnlyList<string> Parse(string output)
    {
        var lines = output.Split(["\r\n", "\n"], StringSplitOptions.None);
        var tests = new List<string>();
        var inList = false;

        foreach (var line in lines)
        {
            if (!inList)
            {
                if (line.Contains("The following Tests are available:", StringComparison.OrdinalIgnoreCase))
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

            if (trimmed.StartsWith("Total tests:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Test Run", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Build ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Results ", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            tests.Add(trimmed);
        }

        return tests;
    }
}
