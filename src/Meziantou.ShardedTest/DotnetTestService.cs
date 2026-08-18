using System.Text;

namespace Meziantou.ShardedTest;

internal static class DotnetTestService
{
    // Microsoft.Testing.Platform reports this exit code when a test module did not run any test. When multiple
    // test modules are involved, a shard rarely contains a test for every module, so the code must be ignored.
    // https://aka.ms/testingplatform/exitcodes
    private const int ZeroTestsRanExitCode = 8;

    public static async Task<TestListResult> ListTestsAsync(string[] forwardArgs, CancellationToken cancellationToken, bool verbose = false)
    {
        var arguments = BuildListTestsArguments(forwardArgs);

        // The output is parsed, so it must not be localized
        var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_CLI_UI_LANGUAGE"] = "en",
        };

        var result = await ProcessRunner.RunAsync("dotnet", arguments, cancellationToken, environmentVariables: environmentVariables, verbose: verbose);
        var output = CombineOutput(result);
        var tests = TestListParser.Parse(output);

        if (tests.Format is TestListFormat.Unknown)
        {
            var message = result.ExitCode == 0
                ? "Cannot parse the output of 'dotnet test --list-tests'."
                : $"'dotnet test --list-tests' failed with exit code {result.ExitCode}.";

            throw new InvalidOperationException(BuildErrorMessage(message, result));
        }

        // Microsoft.Testing.Platform reports a non-zero exit code when the discovery does not find any test, so
        // the exit code must not be reported as an error when at least one test was found.
        if (result.ExitCode != 0 && tests.Tests.Count == 0)
        {
            throw new InvalidOperationException(BuildErrorMessage($"'dotnet test --list-tests' failed with exit code {result.ExitCode}.", result));
        }

        return tests;
    }

    public static async Task<int> RunTestsAsync(
        string[] forwardArgs,
        TestListResult discoveredTests,
        IReadOnlyList<string> selectedTests,
        CancellationToken cancellationToken,
        bool verbose = false)
    {
        var sanitizedArgs = ArgumentUtilities.RemoveFilterArgs(forwardArgs);
        var maxFilterLength = GetMaxFilterLength(sanitizedArgs);
        var filters = TestFilterBuilder.BuildFilters(discoveredTests.Tests, selectedTests, maxFilterLength);

        if (filters.Count == 0)
        {
            return 0;
        }

        var isTestingPlatform = discoveredTests.Format is TestListFormat.MicrosoftTestingPlatform;
        var ignoreZeroTestsExitCode = isTestingPlatform
            && discoveredTests.AssemblyCount > 1
            && !ArgumentUtilities.HasArg(sanitizedArgs, "--ignore-exit-code");

        if (ignoreZeroTestsExitCode)
        {
            Console.WriteLine($"Multiple test modules were discovered, adding '--ignore-exit-code {ZeroTestsRanExitCode}' so modules without any test in this shard do not fail the run.");
        }

        if (filters.Count > 1 && isTestingPlatform)
        {
            Console.WriteLine($"The tests are run in {filters.Count} batches. Reports must use a unique file name per batch (for instance '--report-trx-filename report_{{pid}}.trx'), otherwise each batch overwrites the previous report.");
        }

        for (var filterIndex = 0; filterIndex < filters.Count; filterIndex++)
        {
            if (filters.Count > 1)
            {
                Console.WriteLine($"Running tests (batch {filterIndex + 1}/{filters.Count})");
            }

            var arguments = BuildRunArguments(sanitizedArgs, filters[filterIndex], ignoreZeroTestsExitCode);
            var result = await ProcessRunner.RunAsync("dotnet", arguments, cancellationToken, forwardOutput: true, verbose: verbose);
            if (result.ExitCode != 0)
            {
                return result.ExitCode;
            }
        }

        return 0;
    }

    private static List<string> BuildListTestsArguments(string[] forwardArgs)
    {
        var args = new List<string> { "test" };
        args.AddRange(ArgumentUtilities.RemoveRunOnlyArgs(ArgumentUtilities.RemoveListTestsArgs(forwardArgs)));
        args.Add("--list-tests");
        return args;
    }

    private static List<string> BuildRunArguments(string[] forwardArgs, string filter, bool ignoreZeroTestsExitCode)
    {
        var args = new List<string> { "test" };
        args.AddRange(forwardArgs);
        args.Add("--filter");
        args.Add(filter);

        if (ignoreZeroTestsExitCode)
        {
            args.Add("--ignore-exit-code");
            args.Add(ZeroTestsRanExitCode.ToString(CultureInfo.InvariantCulture));
        }

        return args;
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

    private static string BuildErrorMessage(string message, ProcessResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(message);

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
