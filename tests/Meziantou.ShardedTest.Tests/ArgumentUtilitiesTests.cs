namespace Meziantou.ShardedTest.Tests;

public class ArgumentUtilitiesTests
{
    [Fact]
    public void RemoveRunOnlyArgs_RemovesReportOptions()
    {
        string[] args = ["tests.csproj", "--report-trx", "--report-trx-filename", "report.trx", "--configuration", "Release"];

        var result = ArgumentUtilities.RemoveRunOnlyArgs(args);

        Assert.Equal(["tests.csproj", "--configuration", "Release"], result);
    }

    [Fact]
    public void RemoveRunOnlyArgs_RemovesDiagnosticExtensionOptions()
    {
        string[] args = ["--coverage", "--coverage-output-format", "cobertura", "--hangdump", "--hangdump-timeout", "5m", "--crashdump", "--retry-failed-tests", "3", "--no-build"];

        var result = ArgumentUtilities.RemoveRunOnlyArgs(args);

        Assert.Equal(["--no-build"], result);
    }

    [Fact]
    public void RemoveRunOnlyArgs_SupportsInlineValues()
    {
        string[] args = ["--report-trx-filename=report.trx", "--filter", "DisplayName~Sample"];

        var result = ArgumentUtilities.RemoveRunOnlyArgs(args);

        Assert.Equal(["--filter", "DisplayName~Sample"], result);
    }

    [Fact]
    public void RemoveRunOnlyArgs_KeepsOtherOptions()
    {
        string[] args = ["tests.csproj", "--filter", "DisplayName~Sample", "--no-build", "-p:Foo=Bar"];

        var result = ArgumentUtilities.RemoveRunOnlyArgs(args);

        Assert.Equal(args, result);
    }

    [Theory]
    [InlineData(new string[] { "--ignore-exit-code", "8" }, true)]
    [InlineData(new string[] { "--ignore-exit-code=8" }, true)]
    [InlineData(new string[] { "--no-build" }, false)]
    public void HasArg(string[] args, bool expected)
    {
        Assert.Equal(expected, ArgumentUtilities.HasArg(args, "--ignore-exit-code"));
    }
}
