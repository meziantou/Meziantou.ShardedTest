using System.Diagnostics;
using System.Xml.Linq;
using Meziantou.Framework;

namespace Meziantou.ShardedTest.Tests;

public class FunctionalTests(ToolFixture toolFixture)
{
    [Fact]
    public async Task RunsExpectedShardAndEmitsTrx()
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames());
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
                "--logger", "trx",
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));
        var output = CombineOutput(runResult);
        Xunit.Assert.Contains("Listing all tests...", output, StringComparison.Ordinal);
        Xunit.Assert.Contains($"Found {allTests.Count} tests, running {expectedTests.Count} over {allTests.Count} (shard 1/2)", output, StringComparison.Ordinal);
        Xunit.Assert.Contains("TestResults", output, StringComparison.Ordinal);

        var executedTests = ReadExecutedTests(resultsRoot);
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), executedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Fact]
    public async Task VerboseOptionPrintsDotnetSubprocessCommands()
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames());

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "1",
                "--verbose",
                testProjectPath,
                "--logger", "trx",
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
        Assert.All(verboseLines, line => Xunit.Assert.Contains(testProjectPath, line, StringComparison.Ordinal));
        Assert.Contains(verboseLines, line => line.Contains("--list-tests", StringComparison.Ordinal));
        Assert.Contains(verboseLines, line => line.Contains("--filter", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListTestsOptionOutputsSelectedTestsWithoutExecuting()
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames(), shouldFail: true);
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
        Xunit.Assert.Contains("Listing all tests...", CombineOutput(runResult), StringComparison.Ordinal);

        var listedTests = TestListParser.Parse(CombineOutput(runResult));
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), listedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ListTestsAndRunsHonorUserFilter()
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames());
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

        var listedTests = TestListParser.Parse(CombineOutput(listResult));
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), listedTests.OrderBy(test => test, StringComparer.Ordinal));

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "2",
                testProjectPath,
                "--logger", "trx",
                "--filter", Filter,
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));

        var executedTests = ReadExecutedTests(resultsRoot);
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), executedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ListTestsAndRunsSupportConfigurationAndNoBuild()
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames());
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

        var listedTests = TestListParser.Parse(CombineOutput(listResult));
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), listedTests.OrderBy(test => test, StringComparer.Ordinal));

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "1",
                testProjectPath,
                "--logger", "trx",
                "--configuration", Configuration,
                "--no-build",
            ],
            Path.GetDirectoryName(testProjectPath)!,
            environmentVariables: null);

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));

        var executedTests = ReadExecutedTests(resultsRoot);
        Assert.Equal(expectedTests.OrderBy(test => test, StringComparer.Ordinal), executedTests.OrderBy(test => test, StringComparer.Ordinal));
    }

    [Fact]
    public async Task SplitsFiltersWhenMaxLengthIsSmall()
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames());
        var resultsRoot = Path.GetDirectoryName(testProjectPath)!;
        CleanupTrxResults(resultsRoot);

        var allTests = await ListTestsAsync(testProjectPath);

        var runResult = await RunToolAsync(
            toolPath,
            [
                "--shard-index", "1",
                "--total-shards", "1",
                testProjectPath,
                "--logger", "trx"
            ],
            Path.GetDirectoryName(testProjectPath)!,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["TEST_PARALLELIZATION_MAX_FILTER_LENGTH"] = "60",
            });

        Assert.True(runResult.ExitCode == 0, BuildProcessMessage(runResult));
        Xunit.Assert.Contains("Running tests (batch 1/", CombineOutput(runResult), StringComparison.Ordinal);

        var trxFiles = Directory.GetFiles(resultsRoot, "*.trx", SearchOption.AllDirectories);
        Assert.HasCountGreaterThan(1, trxFiles, "Expected multiple trx files due to filter splitting.");

        var executedTests = ReadExecutedTests(resultsRoot);
        Assert.Equal(allTests.Order(StringComparer.Ordinal), executedTests.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task NoTestsSelected_ReturnsSuccessWithoutExecuting()
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames());
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
        Xunit.Assert.Contains("No tests selected for this job.", output, StringComparison.Ordinal);

        var trxFiles = Directory.GetFiles(resultsRoot, "*.trx", SearchOption.AllDirectories);
        Assert.Empty(trxFiles);
    }

    [Fact]
    public async Task UsesGitLabEnvironmentVariablesAsFallback()
    {
        await using var temp = TemporaryDirectory.Create();
        var toolPath = toolFixture.ToolPath;
        var testProjectPath = CreateTestProject(temp, GetSampleTestNames());
        var resultsRoot = Path.GetDirectoryName(testProjectPath)!;
        CleanupTrxResults(resultsRoot);

        var allTests = await ListTestsAsync(testProjectPath);
        var expectedTests = TestSelector.SelectTests(allTests, shardIndex: 1, totalShards: 2);

        var runResult = await RunToolAsync(
            toolPath,
            [
                testProjectPath,
                "--logger", "trx",
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
        Xunit.Assert.Contains(expectedError, output, StringComparison.Ordinal);
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

    private static string CreateTestProject(TemporaryDirectory temp, IReadOnlyList<string> testNames, bool shouldFail = false)
    {
        var projectDirectory = Path.Combine(temp.FullPath, "SampleTests");
        Directory.CreateDirectory(projectDirectory);

        var projectPath = Path.Combine(projectDirectory, "SampleTests.csproj");
        File.WriteAllText(projectPath, BuildTestProjectFile());

        var testFilePath = Path.Combine(projectDirectory, "SampleTests.cs");
        File.WriteAllText(testFilePath, BuildTestFile(testNames, shouldFail));

        return projectPath;
    }

    private static string BuildTestProjectFile()
    {
        return """
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
""";
    }

    private static string BuildTestFile(IReadOnlyList<string> testNames, bool shouldFail)
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

    private static async Task<IReadOnlyList<string>> ListTestsAsync(string projectPath)
    {
        var result = await RunDotnetAsync(
            ["test", projectPath, "--list-tests"],
            Path.GetDirectoryName(projectPath)!,
            environmentVariables: null);

        Assert.True(result.ExitCode == 0, BuildProcessMessage(result));

        var output = CombineOutput(result);
        return TestListParser.Parse(output);
    }

    private static async Task<IReadOnlyList<string>> ListTestsAsync(string projectPath, string filter)
    {
        var result = await RunDotnetAsync(
            ["test", projectPath, "--list-tests", "--filter", filter],
            Path.GetDirectoryName(projectPath)!,
            environmentVariables: null);

        Assert.True(result.ExitCode == 0, BuildProcessMessage(result));

        var output = CombineOutput(result);
        return TestListParser.Parse(output);
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
        var testMap = document
            .Descendants()
            .Where(element => element.Name.LocalName == "UnitTest")
            .Select(unitTest => new
            {
                Id = unitTest.Attribute("id")?.Value,
                Name = GetFullTestName(unitTest)
            })
            .Where(entry => entry.Id is not null && entry.Name is not null)
            .ToDictionary(entry => entry.Id!, entry => entry.Name!, StringComparer.OrdinalIgnoreCase);

        foreach (var result in document.Descendants().Where(element => element.Name.LocalName == "UnitTestResult"))
        {
            var id = result.Attribute("testId")?.Value;
            if (id is not null && testMap.TryGetValue(id, out var name))
            {
                yield return name;
            }
        }
    }

    private static string? GetFullTestName(XElement unitTest)
    {
        var testMethod = unitTest.Descendants().FirstOrDefault(element => element.Name.LocalName == "TestMethod");
        var className = testMethod?.Attribute("className")?.Value;
        var methodName = testMethod?.Attribute("name")?.Value;

        if (string.IsNullOrWhiteSpace(className) || string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }

        return className + "." + methodName;
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
