namespace Meziantou.ShardedTest.Tests;

public class TestListParserTests
{
    [Fact]
    public void Parse_ExtractsTestsFromListOutput()
    {
        var output = """
Test run for C:\tests\bin\Debug\net8.0\Tests.dll (.NETCoreApp,Version=v8.0)
Microsoft (R) Test Execution Command Line Tool Version 17.10.0
The following Tests are available:
    Sample.Namespace.Tests.TestA
    Sample.Namespace.Tests.TestB
Total tests: 2
""";

        var result = TestListParser.Parse(output);

        Assert.Equal(TestListFormat.VSTest, result.Format);
        Assert.Equal(1, result.AssemblyCount);
        Assert.Equal(["Sample.Namespace.Tests.TestA", "Sample.Namespace.Tests.TestB"], result.Tests);
    }

    [Fact]
    public void Parse_ExtractsTestsFromMultipleVSTestAssemblies()
    {
        var output = """
The following Tests are available:
    Sample.Namespace.Tests.TestA
Total tests: 1
Test run for C:\tests\bin\Debug\net8.0\OtherTests.dll (.NETCoreApp,Version=v8.0)
The following Tests are available:
    Other.Namespace.Tests.TestB
Total tests: 1
""";

        var result = TestListParser.Parse(output);

        Assert.Equal(TestListFormat.VSTest, result.Format);
        Assert.Equal(2, result.AssemblyCount);
        Assert.Equal(["Sample.Namespace.Tests.TestA", "Other.Namespace.Tests.TestB"], result.Tests);
    }

    [Fact]
    public void Parse_ExtractsTestsFromMicrosoftTestingPlatformOutput()
    {
        var output = """
Discovering tests from /repo/bin/Debug/net10.0/Tests.dll (net10.0|arm64)

Discovered 3 tests in assembly - /repo/bin/Debug/net10.0/Tests.dll (net10.0|arm64)
  Sample.Namespace.Tests.TestA
  Sample.Namespace.Tests.TestB
  Sample.Namespace.Tests.TheoryA(value: 1)

Discovered 3 tests.
""";

        var result = TestListParser.Parse(output);

        Assert.Equal(TestListFormat.MicrosoftTestingPlatform, result.Format);
        Assert.Equal(1, result.AssemblyCount);
        Assert.Equal(
            ["Sample.Namespace.Tests.TestA", "Sample.Namespace.Tests.TestB", "Sample.Namespace.Tests.TheoryA(value: 1)"],
            result.Tests);
    }

    [Fact]
    public void Parse_ExtractsTestsFromMultipleMicrosoftTestingPlatformAssemblies()
    {
        var output = """
Discovering tests from /repo/bin/Debug/net10.0/A.dll (net10.0|arm64)
Discovering tests from /repo/bin/Debug/net10.0/B.dll (net10.0|arm64)

Discovered 1 tests in assembly - /repo/bin/Debug/net10.0/A.dll (net10.0|arm64)
  A.Tests.TestA

Discovered 1 tests in assembly - /repo/bin/Debug/net10.0/B.dll (net10.0|arm64)
  B.Tests.TestB

Discovered 2 tests in 2 assemblies.
""";

        var result = TestListParser.Parse(output);

        Assert.Equal(TestListFormat.MicrosoftTestingPlatform, result.Format);
        Assert.Equal(2, result.AssemblyCount);
        Assert.Equal(["A.Tests.TestA", "B.Tests.TestB"], result.Tests);
    }

    [Fact]
    public void Parse_CountsAssemblies_WhenTheTestsOfAllAssembliesAreReportedInASingleBlock()
    {
        var output = """
Discovering tests from /repo/A/bin/Debug/net10.0/A.dll (net10.0|arm64)
(try 2) Discovering tests from /repo/B/bin/Debug/net10.0/B.dll (net10.0|arm64)

Discovered 2 tests in assembly - /repo/A/bin/Debug/net10.0/A.dll (net10.0|arm64)
  A.Tests.TestA
  B.Tests.TestB

Discovered 2 tests.
""";

        var result = TestListParser.Parse(output);

        Assert.Equal(TestListFormat.MicrosoftTestingPlatform, result.Format);
        Assert.Equal(2, result.AssemblyCount);
        Assert.Equal(["A.Tests.TestA", "B.Tests.TestB"], result.Tests);
    }

    [Fact]
    public void Parse_MicrosoftTestingPlatformWithoutTest_IsRecognized()
    {
        var output = """
Discovering tests from /repo/bin/Debug/net10.0/Tests.dll (net10.0|arm64)

Discovered 0 tests.

Test discovery completed with non-success exit code: 8 (see: https://aka.ms/testingplatform/exitcodes)
""";

        var result = TestListParser.Parse(output);

        Assert.Equal(TestListFormat.MicrosoftTestingPlatform, result.Format);
        Assert.Empty(result.Tests);
    }

    [Fact]
    public void Parse_WithoutHeader_ReturnsUnknownFormat()
    {
        var output = "Test run for C:\\tests\\bin\\Debug\\net8.0\\Tests.dll";

        var result = TestListParser.Parse(output);

        Assert.Equal(TestListFormat.Unknown, result.Format);
        Assert.Empty(result.Tests);
    }
}
