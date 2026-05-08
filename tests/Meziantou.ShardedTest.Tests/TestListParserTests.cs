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

        var tests = TestListParser.Parse(output);

        Assert.Equal(
            [
                new DiscoveredTest("net8.0", "Sample.Namespace.Tests.TestA"),
                new DiscoveredTest("net8.0", "Sample.Namespace.Tests.TestB"),
            ],
            tests);
    }

    [Fact]
    public void Parse_WithoutHeader_ReturnsEmptyList()
    {
        var output = "Test run for C:\\tests\\bin\\Debug\\net8.0\\Tests.dll";

        var tests = TestListParser.Parse(output);

        Assert.Empty(tests);
    }

    [Fact]
    public void Parse_MultiTargetOutput_AssignsFrameworkPerSection()
    {
        var output = """
Test run for C:\tests\bin\Debug\net8.0\Tests.dll (.NETCoreApp,Version=v8.0)
The following Tests are available:
    Sample.Namespace.Tests.TestA
Total tests: 1
Test run for C:\tests\bin\Debug\net10.0\Tests.dll (.NETCoreApp,Version=v10.0)
The following Tests are available:
    Sample.Namespace.Tests.TestA
Total tests: 1
""";

        var tests = TestListParser.Parse(output);

        Assert.Equal(
            [
                new DiscoveredTest("net8.0", "Sample.Namespace.Tests.TestA"),
                new DiscoveredTest("net10.0", "Sample.Namespace.Tests.TestA"),
            ],
            tests);
    }

    [Fact]
    public void Parse_TupleLines_UsesFrameworkFromTuple()
    {
        var output = """
The following Tests are available:
    (net8.0, Sample.Namespace.Tests.TestA)
    (net10.0, Sample.Namespace.Tests.TestB)
Total tests: 2
""";

        var tests = TestListParser.Parse(output);

        Assert.Equal(
            [
                new DiscoveredTest("net8.0", "Sample.Namespace.Tests.TestA"),
                new DiscoveredTest("net10.0", "Sample.Namespace.Tests.TestB"),
            ],
            tests);
    }
}
