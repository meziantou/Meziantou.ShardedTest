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

        Assert.Equal(["Sample.Namespace.Tests.TestA", "Sample.Namespace.Tests.TestB"], tests);
    }

    [Fact]
    public void Parse_WithoutHeader_ReturnsEmptyList()
    {
        var output = "Test run for C:\\tests\\bin\\Debug\\net8.0\\Tests.dll";

        var tests = TestListParser.Parse(output);

        Assert.Empty(tests);
    }
}
