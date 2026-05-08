namespace Meziantou.ShardedTest.Tests;

public class TestSelectorTests
{
    [Fact]
    public void SelectTests_UsesDeterministicOrderingAndJobIndex()
    {
        var tests = new[] { "B.Test", "A.Test", "D.Test", "C.Test" };

        var selected = TestSelector.SelectTests(tests, shardIndex: 1, totalShards: 2);

        Assert.Equal(["A.Test", "C.Test"], selected);
    }

    [Fact]
    public void SelectTests_SelectsSecondJobTests()
    {
        var tests = new[] { "B.Test", "A.Test", "D.Test", "C.Test" };

        var selected = TestSelector.SelectTests(tests, shardIndex: 2, totalShards: 2);

        Assert.Equal(["B.Test", "D.Test"], selected);
    }

    [Fact]
    public void SelectTests_ReturnsEmpty_WhenNoTestsProvided()
    {
        var selected = TestSelector.SelectTests(Array.Empty<string>(), shardIndex: 1, totalShards: 3);

        Assert.Empty(selected);
    }

    [Fact]
    public void SelectTests_ForFrameworkAwareTests_UsesFrameworkThenNameOrdering()
    {
        var tests = new[]
        {
            new DiscoveredTest("net10.0", "A.Test"),
            new DiscoveredTest("net8.0", "B.Test"),
            new DiscoveredTest("net8.0", "A.Test"),
            new DiscoveredTest("net10.0", "B.Test"),
        };

        var selected = TestSelector.SelectTests(tests, shardIndex: 1, totalShards: 2);

        Assert.Equal(
            [
                new DiscoveredTest("net10.0", "A.Test"),
                new DiscoveredTest("net8.0", "A.Test"),
            ],
            selected);
    }
}
