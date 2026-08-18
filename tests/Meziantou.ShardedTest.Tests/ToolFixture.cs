using System.Diagnostics;
using System.Xml.Linq;
using Meziantou.Framework;

namespace Meziantou.ShardedTest.Tests;

public sealed class ToolFixture : IAsyncLifetime
{
    // The version must not exist on NuGet.org, otherwise "dotnet tool install" can silently install the
    // published tool instead of the one built from the sources
    private const string ToolVersion = "999.0.0-functionaltests";

    private TemporaryDirectory? _temp;

    public string ToolPath { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _temp = TemporaryDirectory.Create();
        ToolPath = await BuildAndInstallToolAsync(_temp);
    }

    public async ValueTask DisposeAsync()
    {
        if (_temp is null)
            return;

        await _temp.DisposeAsync();
        _temp = null;
    }

    private static async Task<string> BuildAndInstallToolAsync(TemporaryDirectory temp)
    {
        var appProjectPath = GetAppProjectPath();
        var repoRoot = GetRepoRoot();
        var packageOutput = Path.Combine(temp.FullPath, "packages");
        Directory.CreateDirectory(packageOutput);

        var packResult = await RunDotnetAsync(
            ["pack", appProjectPath, "--output", packageOutput, "--nologo", "-p:Version=" + ToolVersion],
            repoRoot,
            environmentVariables: null);
        Assert.True(packResult.ExitCode == 0, BuildProcessMessage(packResult));

        var packageId = GetProjectProperty(appProjectPath, "PackageId")
            ?? GetProjectProperty(appProjectPath, "AssemblyName")
            ?? Path.GetFileNameWithoutExtension(appProjectPath);
        var toolName = GetProjectProperty(appProjectPath, "ToolName") ?? packageId;

        var toolInstallPath = Path.Combine(temp.FullPath, "tool");
        Directory.CreateDirectory(toolInstallPath);

        var installResult = await RunDotnetAsync(
            [
                "tool", "install",
                "--tool-path", toolInstallPath,
                "--add-source", packageOutput,
                "--ignore-failed-sources",
                "--version", ToolVersion,
                packageId
            ],
            repoRoot,
            environmentVariables: null);
        Assert.True(installResult.ExitCode == 0, BuildProcessMessage(installResult));

        var toolExecutablePath = GetToolExecutablePath(toolInstallPath, toolName);
        Assert.True(File.Exists(toolExecutablePath), $"Tool executable not found: {toolExecutablePath}");
        return toolExecutablePath;
    }

    private static string GetAppProjectPath()
    {
        return Path.Combine(GetRepoRoot(), "src", "Meziantou.ShardedTest", "Meziantou.ShardedTest.csproj");
    }

    internal static FullPath GetRepoRoot()
    {
        var directory = FullPath.FromPath(AppContext.BaseDirectory);
        if (directory.TryFindFirstAncestorOrSelf(dir => File.Exists(dir / "Meziantou.ShardedTest.slnx"), out var repoRoot))
            return repoRoot;

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string? GetProjectProperty(string projectPath, string propertyName)
    {
        var document = XDocument.Load(projectPath);
        var value = document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == propertyName)
            ?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetToolExecutablePath(string toolInstallPath, string toolName)
    {
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        return Path.Combine(toolInstallPath, toolName + extension);
    }

    private static async Task<ProcessResult> RunDotnetAsync(IReadOnlyList<string> args, string workingDirectory, Dictionary<string, string>? environmentVariables)
    {
        return await RunProcessAsync("dotnet", args, workingDirectory, environmentVariables);
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, IReadOnlyList<string> args, string workingDirectory, Dictionary<string, string>? environmentVariables)
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
}
