using System.Text;

namespace Meziantou.ShardedTest;

internal static class DotnetTestService
{

    public static async Task<IReadOnlyList<DiscoveredTest>> ListTestsAsync(string[] forwardArgs, CancellationToken cancellationToken, bool verbose = false)
    {
        var arguments = BuildListTestsArguments(forwardArgs);
        var result = await ProcessRunner.RunAsync("dotnet", arguments, cancellationToken, verbose: verbose);
        if (result.ExitCode != 0)
        {
            var message = BuildErrorMessage("dotnet test --list-tests", result);
            throw new InvalidOperationException(message);
        }

        var output = CombineOutput(result);
        return TestListParser.Parse(output);
    }

    public static async Task<int> RunTestsAsync(
        string[] forwardArgs,
        IReadOnlyList<DiscoveredTest> allTests,
        IReadOnlyList<DiscoveredTest> selectedTests,
        CancellationToken cancellationToken,
        bool verbose = false)
    {
        var sanitizedArgs = ArgumentUtilities.RemoveFilterArgs(forwardArgs);
        var frameworkArgs = ArgumentUtilities.RemoveFrameworkArgs(sanitizedArgs);
        var maxFilterLength = GetMaxFilterLength(frameworkArgs);
        var frameworkGroups = selectedTests
            .GroupBy(test => test.TargetFramework, StringComparer.Ordinal)
            .OrderBy(group => group.Key ?? string.Empty, StringComparer.Ordinal);

        foreach (var frameworkGroup in frameworkGroups)
        {
            var allFrameworkTests = allTests
                .Where(test => string.Equals(test.TargetFramework, frameworkGroup.Key, StringComparison.Ordinal))
                .Select(test => test.Name)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var selectedFrameworkTests = frameworkGroup
                .Select(test => test.Name)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var filters = TestFilterBuilder.BuildFilters(allFrameworkTests, selectedFrameworkTests, maxFilterLength);
            var args = AddFrameworkArg(frameworkArgs, frameworkGroup.Key);

            for (var filterIndex = 0; filterIndex < filters.Count; filterIndex++)
            {
                if (filters.Count > 1)
                {
                    Console.WriteLine($"Running tests (batch {filterIndex + 1}/{filters.Count})");
                }

                var arguments = BuildRunArguments(args, filters[filterIndex]);
                var result = await ProcessRunner.RunAsync("dotnet", arguments, cancellationToken, forwardOutput: true, verbose: verbose);
                if (result.ExitCode != 0)
                {
                    return result.ExitCode;
                }
            }
        }

        return 0;
    }

    private static List<string> BuildListTestsArguments(string[] forwardArgs)
    {
        var args = new List<string> { "test" };
        args.AddRange(ArgumentUtilities.RemoveListTestsArgs(forwardArgs));
        args.Add("--list-tests");
        return args;
    }

    private static List<string> BuildRunArguments(string[] forwardArgs, string filter)
    {
        var args = new List<string> { "test" };
        args.AddRange(forwardArgs);
        args.Add("--filter");
        args.Add(filter);
        return args;
    }

    private static string[] AddFrameworkArg(string[] forwardArgs, string? targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return forwardArgs;
        }

        return [.. forwardArgs, "--framework", targetFramework];
    }

    private static int GetMaxFilterLength(IReadOnlyList<string> forwardArgs)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("TEST_PARALLELIZATION_MAX_FILTER_LENGTH"), CultureInfo.InvariantCulture, out var overrideValue) && overrideValue > 0)
        {
            return overrideValue;
        }

        const int MaxCommandLineLength = 32767;
        const int SafetyMargin = 256;

        var argumentList = new List<string> { "test" };
        argumentList.AddRange(forwardArgs);
        argumentList.Add("--filter");

        var baseLength = "dotnet".Length;
        foreach (var arg in argumentList)
        {
            baseLength += 1 + arg.Length;
        }

        var available = MaxCommandLineLength - baseLength - SafetyMargin;
        return Math.Max(1, available);
    }

    private static string CombineOutput(ProcessResult result)
    {
        if (string.IsNullOrWhiteSpace(result.StandardError))
        {
            return result.StandardOutput;
        }

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return result.StandardError;
        }

        return result.StandardOutput + Environment.NewLine + result.StandardError;
    }

    private static string BuildErrorMessage(string command, ProcessResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"{command} failed with exit code {result.ExitCode}.");

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            builder.AppendLine(result.StandardOutput.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            builder.AppendLine(result.StandardError.TrimEnd());
        }

        return builder.ToString();
    }
}
