namespace Meziantou.ShardedTest;

internal static class TestListParser
{
    private const string VSTestHeader = "The following Tests are available:";
    private const string MicrosoftTestingPlatformPrefix = "Discovered ";
    private const string MicrosoftTestingPlatformAssemblyMarker = " in assembly - ";
    private const string MicrosoftTestingPlatformDiscoveringMarker = "Discovering tests from ";

    public static TestListResult Parse(string output)
    {
        var lines = output.Split(["\r\n", "\n"], StringSplitOptions.None);
        return ParseVSTest(lines) ?? ParseMicrosoftTestingPlatform(lines) ?? TestListResult.Unknown;
    }

    // VSTest prints a single block per test assembly:
    //   The following Tests are available:
    //       Namespace.ClassName.MethodName
    //   Total tests: 1
    private static TestListResult? ParseVSTest(string[] lines)
    {
        var tests = new List<string>();
        var assemblyCount = 0;
        var index = 0;

        while (index < lines.Length)
        {
            if (!lines[index++].Contains(VSTestHeader, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            assemblyCount++;
            while (index < lines.Length)
            {
                var line = lines[index];
                if (string.IsNullOrWhiteSpace(line))
                {
                    index++;
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
                index++;
            }
        }

        return assemblyCount == 0 ? null : new TestListResult(TestListFormat.VSTest, tests, assemblyCount);
    }

    // Microsoft.Testing.Platform prints one block per test assembly:
    //   Discovering tests from /path/to/Tests.dll (net10.0|arm64)
    //
    //   Discovered 1 tests in assembly - /path/to/Tests.dll (net10.0|arm64)
    //     Namespace.ClassName.MethodName
    //
    //   Discovered 1 tests.
    private static TestListResult? ParseMicrosoftTestingPlatform(string[] lines)
    {
        var tests = new List<string>();

        // The tests of several assemblies may be reported in a single block, so the assemblies are also
        // collected from the lines reporting the beginning of the discovery.
        var assemblies = new HashSet<string>(StringComparer.Ordinal);
        var recognized = false;
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index++];
            var discoveringIndex = line.IndexOf(MicrosoftTestingPlatformDiscoveringMarker, StringComparison.Ordinal);
            if (discoveringIndex >= 0)
            {
                assemblies.Add(line[(discoveringIndex + MicrosoftTestingPlatformDiscoveringMarker.Length)..]);
                continue;
            }

            if (!line.StartsWith(MicrosoftTestingPlatformPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            recognized = true;
            var assemblyIndex = line.IndexOf(MicrosoftTestingPlatformAssemblyMarker, StringComparison.Ordinal);
            if (assemblyIndex < 0)
            {
                continue;
            }

            assemblies.Add(line[(assemblyIndex + MicrosoftTestingPlatformAssemblyMarker.Length)..]);
            while (index < lines.Length)
            {
                var testLine = lines[index];
                if (string.IsNullOrWhiteSpace(testLine) || !testLine.StartsWith("  ", StringComparison.Ordinal))
                {
                    break;
                }

                tests.Add(testLine.Trim());
                index++;
            }
        }

        return recognized ? new TestListResult(TestListFormat.MicrosoftTestingPlatform, tests, assemblies.Count) : null;
    }
}
