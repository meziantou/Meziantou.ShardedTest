using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Meziantou.Framework;

namespace Meziantou.ShardedTest.Tests;

public class FunctionalTests(ToolFixture toolFixture)
{
    public static TheoryData<TestRunner> Runners => new(Enum.GetValues<TestRunner>());

    [Theory]
    [MemberData(nameof(Runners))]
    public async Task RunsExpectedShardAndEmitsTrx(TestRunner runner)
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames(), runner);
        var resultsRoot = Path.GetDirectoryName(testProjectPath)!;
        CleanupTrxResults(resultsRoot);

        var allTests = await ListTestsAsync(testProjectPath);
        var expectedTests = TestSelector.SelectTests(allTests, shardIndex: 1, totalShards: 2);

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "2",
                testProjectPath,
                .. GetReportArgs(runner),
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));
        var output = CombineOutput(runResult);
        Assert.Contains("Listing all tests...", output);
        Assert.Contains($"Found {allTests.Count} tests, running {expectedTests.Count} over {allTests.Count} (shard 1/2)", output);
        Assert.Contains("SampleTests.dll", output);

        var executedTests = ReadExecutedTests(resultsRoot);
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), executedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Runners))]
    public async Task ShardsIndividualTheoryTestCases(TestRunner runner)
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames(), runner, includeTheory: true);
        var resultsRoot = Path.GetDirectoryName(testProjectPath)!;
        CleanupTrxResults(resultsRoot);

        var allTests = await ListTestsAsync(testProjectPath);
        Assert.Contains(allTests, test => test.Contains('(', StringComparison.Ordinal));

        var expectedTests = TestSelector.SelectTests(allTests, shardIndex: 2, totalShards: 3);
        Assert.Contains(expectedTests, test => test.Contains('(', StringComparison.Ordinal));

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "2",
                "--total-shards", "3",
                testProjectPath,
                .. GetReportArgs(runner),
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));

        var executedTests = ReadExecutedTests(resultsRoot);
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), executedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ShardsSolutionWithSeveralTestProjects()
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;

        // Microsoft.Testing.Platform reports an error when a test module of the solution does not run any test
        var solutionPath = CreateTestSolution(temp, TestRunner.MicrosoftTestingPlatform);
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        CleanupTrxResults(solutionDirectory);

        var allTests = await ListTestsAsync(["test", "--solution", solutionPath, "--list-tests"], solutionDirectory);
        var expectedTests = TestSelector.SelectTests(allTests, shardIndex: 1, totalShards: 4);
        Assert.Single(expectedTests, "The shard must only contain tests from one of the test projects.");

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "4",
                "--solution", solutionPath,
                .. GetReportArgs(TestRunner.MicrosoftTestingPlatform),
            ],
            solutionDirectory,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));

        var executedTests = ReadExecutedTests(solutionDirectory);
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), executedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Runners))]
    public async Task VerboseOptionPrintsDotnetSubprocessCommands(TestRunner runner)
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames(), runner);

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "1",
                "--verbose",
                testProjectPath,
                .. GetReportArgs(runner),
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));

        var output = CombineOutput(runResult);
        var verboseLines = output
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("Executing: dotnet ", StringComparison.Ordinal))
            .ToArray();

        Assert.HasCountGreaterThanOrEqual(2, verboseLines, "Expected at least two verbose dotnet command lines.");
        Assert.All(verboseLines, line => Assert.Contains(testProjectPath, line));
        Assert.Contains(verboseLines, line => line.Contains("--list-tests", StringComparison.Ordinal));
        Assert.Contains(verboseLines, line => line.Contains("--filter", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Runners))]
    public async Task ListTestsOptionOutputsSelectedTestsWithoutExecuting(TestRunner runner)
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames(), runner, shouldFail: true);
        var resultsRoot = Path.GetDirectoryName(testProjectPath)!;
        CleanupTrxResults(resultsRoot);

        var allTests = await ListTestsAsync(testProjectPath);
        var expectedTests = TestSelector.SelectTests(allTests, shardIndex: 1, totalShards: 2);

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "2",
                testProjectPath,
                "--list-tests",
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));
        Assert.Contains("Listing all tests...", CombineOutput(runResult));

        var listedTests = TestListParser.Parse(CombineOutput(runResult)).Tests;
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), listedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Runners))]
    public async Task ListTestsAndRunsHonorUserFilter(TestRunner runner)
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames(), runner);
        var resultsRoot = Path.GetDirectoryName(testProjectPath)!;
        CleanupTrxResults(resultsRoot);

        const string Filter = "FullyQualifiedName~ShardB";

        var filteredTests = await ListTestsAsync(testProjectPath, Filter);
        var expectedTests = TestSelector.SelectTests(filteredTests, shardIndex: 1, totalShards: 2);

        var listResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "2",
                testProjectPath,
                "--list-tests",
                "--filter", Filter,
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(listResult.ExitCode == 0, BuildProcessMessage(listResult));

        var listedTests = TestListParser.Parse(CombineOutput(listResult)).Tests;
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), listedTests.OrderBy(test => test, StringComparer.Ordinal));

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "2",
                testProjectPath,
                .. GetReportArgs(runner),
                "--filter", Filter,
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));

        var executedTests = ReadExecutedTests(resultsRoot);
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), executedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Runners))]
    public async Task ListTestsAndRunsSupportConfigurationAndNoBuild(TestRunner runner)
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames(), runner);
        var resultsRoot = Path.GetDirectoryName(testProjectPath)!;
        CleanupTrxResults(resultsRoot);

        const string Configuration = "Release";

        await BuildProjectAsync(testProjectPath, Configuration);
        IntroduceBuildError(testProjectPath);

        var expectedTests = GetSampleTestNames();

        var listResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "1",
                testProjectPath,
                "--list-tests",
                "--configuration", Configuration,
                "--no-build",
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(listResult.ExitCode == 0, BuildProcessMessage(listResult));

        var listedTests = TestListParser.Parse(CombineOutput(listResult)).Tests;
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), listedTests.OrderBy(test => test, StringComparer.Ordinal));

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "1",
                testProjectPath,
                .. GetReportArgs(runner),
                "--configuration", Configuration,
                "--no-build",
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));

        var executedTests = ReadExecutedTests(resultsRoot);
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), executedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Runners))]
    public async Task SplitsFiltersWhenMaxLengthIsSmall(TestRunner runner)
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames(), runner);
        var resultsRoot = Path.GetDirectoryName(testProjectPath)!;
        CleanupTrxResults(resultsRoot);

        var allTests = await ListTestsAsync(testProjectPath);

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "1",
                testProjectPath,
                // Each batch must use a distinct report file name, otherwise a batch overwrites the previous report
                .. GetReportArgs(runner, reportFileName: "report_{pid}.trx"),
            ],
            Path.GetDirectoryName(testProjectPath)!,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["TEST_PARALLELIZATION_MAX_FILTER_LENGTH"] = "30",
            });

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));
        Assert.Contains("Running tests (batch 1/", CombineOutput(runResult));

        var trxFiles = Directory.GetFiles(resultsRoot, "*.trx", SearchOption.AllDirectories);
        Assert.HasCountGreaterThan(1, trxFiles, "Expected multiple trx files due to filter splitting.");

        var executedTests = ReadExecutedTests(resultsRoot);
        Assert.Equal(allTests.Order(StringComparer.Ordinal), executedTests.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Runners))]
    public async Task NoTestsSelected_ReturnsSuccessWithoutExecuting(TestRunner runner)
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames(), runner);
        var resultsRoot = Path.GetDirectoryName(testProjectPath)!;
        CleanupTrxResults(resultsRoot);

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "10",
                "--total-shards", "10",
                testProjectPath
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));

        var output = CombineOutput(runResult);
        Assert.Contains("No tests selected for this job.", output);

        var trxFiles = Directory.GetFiles(resultsRoot, "*.trx", SearchOption.AllDirectories);
        Assert.Empty(trxFiles);
    }

    [Theory]
    [MemberData(nameof(Runners))]
    public async Task UsesGitLabEnvironmentVariablesAsFallback(TestRunner runner)
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames(), runner);
        var resultsRoot = Path.GetDirectoryName(testProjectPath)!;
        CleanupTrxResults(resultsRoot);

        var allTests = await ListTestsAsync(testProjectPath);
        var expectedTests = TestSelector.SelectTests(allTests, shardIndex: 1, totalShards: 2);

        var runResult = await RunToolAsync(
            toolPath,
            [
                testProjectPath,
                .. GetReportArgs(runner),
            ],
            Path.GetDirectoryName(testProjectPath)!,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CI_NODE_INDEX"] = "1",
                ["CI_NODE_TOTAL"] = "2",
            });

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));

        var executedTests = ReadExecutedTests(resultsRoot);
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), executedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(GetInvalidArguments))]
    public async Task RejectsInvalidJobOrTotalValues(string[] args, string expectedError)
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;

        var runResult = await RunToolAsync(toolPath, args, temp.FullPath, environmentVariables: null);

        Assert.True(runResult.ExitCode != 0, BuildProcessMessage(runResult));

        var output = CombineOutput(runResult);
        Assert.Contains(expectedError, output);
    }

    public static IEnumerable<object[]> GetInvalidArguments()
    {
        yield return
        [
            new[] { "--shard-index", "0", "--total-shards", "1" },
            "--shard-index must be greater than zero."
        ];

        yield return
        [
            new[] { "--shard-index", "2", "--total-shards", "1" },
            "--shard-index must be less than or equal to --total-shards."
        ];

        yield return
        [
            new[] { "--shard-index", "1", "--total-shards", "0" },
            "--total-shards must be greater than zero."
        ];

        yield return
        [
            new[] { "--shard-index", "a", "--total-shards", "1" },
            "The --shard-index option must be an integer."
        ];

        yield return
        [
            new[] { "--shard-index", "1", "--total-shards", "b" },
            "The --total-shards option must be an integer."
        ];
    }

    private static IReadOnlyList<string> GetSampleTestNames() =>
        [
            "ShardA.AlphaTests.Test1",
            "ShardA.AlphaTests.Test2",
            "ShardB.BetaTests.Test1",
            "ShardB.BetaTests.Test2",
            "ShardC.GammaTests.Test1",
            "ShardC.GammaTests.Test2",
        ];

    private static string[] GetReportArgs(TestRunner runner, string? reportFileName = null)
    {
        if (runner is TestRunner.MicrosoftTestingPlatform)
        {
            return reportFileName is null
                ? ["--report-trx"]
                : ["--report-trx", "--report-trx-filename", reportFileName];
        }

        return ["--logger", "trx"];
    }

    private static string CreateTestProject(
        TemporaryDirectory temp,
        IReadOnlyList<string> testNames,
        TestRunner runner,
        bool shouldFail = false,
        bool includeTheory = false)
    {
        var projectDirectory = Path.Combine(temp.FullPath, "SampleTests");
        Directory.CreateDirectory(projectDirectory);

        var projectPath = Path.Combine(projectDirectory, "SampleTests.csproj");
        File.WriteAllText(projectPath, BuildTestProjectFile(runner));

        // The runner used by "dotnet test" is configured in global.json
        File.WriteAllText(Path.Combine(projectDirectory, "global.json"), GetGlobalJson(runner));

        var testFilePath = Path.Combine(projectDirectory, "SampleTests.cs");
        File.WriteAllText(testFilePath, BuildTestFile(testNames, shouldFail, includeTheory));

        return projectPath;
    }

    private static string CreateTestSolution(TemporaryDirectory temp, TestRunner runner)
    {
        var solutionDirectory = Path.Combine(temp.FullPath, "SampleSolution");
        Directory.CreateDirectory(solutionDirectory);

        File.WriteAllText(Path.Combine(solutionDirectory, "global.json"), GetGlobalJson(runner));

        var projectNames = new[] { "First", "Second" };
        foreach (var projectName in projectNames)
        {
            var projectDirectory = Path.Combine(solutionDirectory, projectName);
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, projectName + ".csproj"), BuildTestProjectFile(runner));
            File.WriteAllText(
                Path.Combine(projectDirectory, "Tests.cs"),
                BuildTestFile([$"{projectName}.{projectName}Tests.Test1", $"{projectName}.{projectName}Tests.Test2"], shouldFail: false, includeTheory: false));
        }

        var solutionPath = Path.Combine(solutionDirectory, "SampleSolution.slnx");
        var projects = string.Join(Environment.NewLine, projectNames.Select(name => $"""  <Project Path="{name}/{name}.csproj" />"""));
        File.WriteAllText(solutionPath, $"""
<Solution>
{projects}
</Solution>
""");

        return solutionPath;
    }

    private static string GetGlobalJson(TestRunner runner)
    {
        var runnerName = runner switch
        {
            TestRunner.MicrosoftTestingPlatform => "Microsoft.Testing.Platform",
            _ => "VSTest",
        };

        // The sample projects must use the same SDK as the repository, otherwise a newer SDK installed on the
        // machine would be used and the behavior of "dotnet test" could be different
        using var document = JsonDocument.Parse(File.ReadAllText(ToolFixture.GetRepoRoot() / "global.json"));
        var sdk = document.RootElement.GetProperty("sdk").GetRawText();

        return $$"""{ "sdk": {{sdk}}, "test": { "runner": "{{runnerName}}" } }""";
    }

    private static string BuildTestProjectFile(TestRunner runner)
    {
        return runner switch
        {
            TestRunner.MicrosoftTestingPlatform => """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3.mtp-v2" Version="4.0.0" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="2.3.3" />
  </ItemGroup>
</Project>
""",
            _ => """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
</Project>
""",
        };
    }

    private static string BuildTestFile(IReadOnlyList<string> testNames, bool shouldFail, bool includeTheory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using Xunit;");

        var grouped = testNames
            .Select(ParseTestName)
            .GroupBy(info => new { info.NamespaceName, info.ClassName });

        foreach (var group in grouped)
        {
            builder.AppendLine();
            builder.AppendLine($"namespace {group.Key.NamespaceName}");
            builder.AppendLine("{");
            builder.AppendLine($"    public class {group.Key.ClassName}");
            builder.AppendLine("    {");

            foreach (var test in group)
            {
                builder.AppendLine("        [Fact]");
                builder.AppendLine($"        public void {test.MethodName}() => Assert.True({(shouldFail ? "false" : "true")});");
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");
        }

        if (includeTheory)
        {
            // The display name of a theory test case contains characters that must be escaped in a test filter
            builder.AppendLine();
            builder.AppendLine("""
namespace ShardD
{
    public class DeltaTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void Theory1(int value) => Assert.True(value > 0);
    }
}
""");
        }

        return builder.ToString();
    }

    private static (string NamespaceName, string ClassName, string MethodName) ParseTestName(string fullyQualifiedName)
    {
        var segments = fullyQualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            throw new InvalidOperationException($"Invalid test name '{fullyQualifiedName}'.");
        }

        var methodName = segments[^1];
        var className = segments[^2];
        var namespaceName = string.Join('.', segments[..^2]);
        return (namespaceName, className, methodName);
    }

    private static Task<IReadOnlyList<string>> ListTestsAsync(string projectPath)
    {
        return ListTestsAsync(["test", projectPath, "--list-tests"], Path.GetDirectoryName(projectPath)!);
    }

    private static Task<IReadOnlyList<string>> ListTestsAsync(string projectPath, string filter)
    {
        return ListTestsAsync(["test", projectPath, "--list-tests", "--filter", filter], Path.GetDirectoryName(projectPath)!);
    }

    private static async Task<IReadOnlyList<string>> ListTestsAsync(IReadOnlyList<string> args, string workingDirectory)
    {
        var result = await RunDotnetAsync(args, workingDirectory, environmentVariables: null);

        Assert.True(result.ExitCode == 0, BuildProcessMessage(result));

        var output = CombineOutput(result);
        return TestListParser.Parse(output).Tests;
    }

    private static async Task BuildProjectAsync(string projectPath, string configuration)
    {
        var result = await RunDotnetAsync(
            ["build", projectPath, "--configuration", configuration],
            Path.GetDirectoryName(projectPath)!,
            environmentVariables: null);

        Assert.True(result.ExitCode == 0, BuildProcessMessage(result));
    }

    private static void IntroduceBuildError(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var testFilePath = Path.Combine(projectDirectory, "SampleTests.cs");
        File.AppendAllText(testFilePath, Environment.NewLine + "#error Build should not run" + Environment.NewLine);
    }

    private static string[] ReadExecutedTests(string resultsRoot)
    {
        var files = Directory.GetFiles(resultsRoot, "*.trx", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        var results = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var test in ReadTestsFromTrx(file))
            {
                results.Add(test);
            }
        }

        return results.ToArray();
    }

    private static IEnumerable<string> ReadTestsFromTrx(string filePath)
    {
        var document = XDocument.Load(filePath);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "UnitTestResult")
            .Select(element => element.Attribute("testName")?.Value)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!);
    }

    private static async Task<ProcessResult> RunDotnetAsync(
        IReadOnlyList<string> args,
        string workingDirectory,
        Dictionary<string, string>? environmentVariables)
    {
        return await RunProcessAsync("dotnet", args, workingDirectory, environmentVariables);
    }

    private static async Task<ProcessResult> RunToolAsync(
        string toolPath,
        IReadOnlyList<string> args,
        string workingDirectory,
        Dictionary<string, string>? environmentVariables)
    {
        return await RunProcessAsync(toolPath, args, workingDirectory, environmentVariables);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        Dictionary<string, string>? environmentVariables)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // MSBuild uses this environment variable as a global property. The CI sets it, and "dotnet test --solution"
        // then builds the test projects in another directory than the one it uses to run them.
        startInfo.Environment.Remove("Configuration");

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (environmentVariables is not null)
        {
            foreach (var pair in environmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return new ProcessResult(process.ExitCode, output, error);
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

    private static string BuildProcessMessage(ProcessResult result)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"Exit code: {result.ExitCode}",
                result.StandardOutput,
                result.StandardError
            }.Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static void CleanupTrxResults(string resultsRoot)
    {
        var testResultsPath = Path.Combine(resultsRoot, "TestResults");
        if (Directory.Exists(testResultsPath))
        {
            Directory.Delete(testResultsPath, recursive: true);
        }

        foreach (var file in Directory.GetFiles(resultsRoot, "*.trx", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }
    }
}
