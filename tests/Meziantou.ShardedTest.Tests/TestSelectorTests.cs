namespace Meziantou.ShardedTest.Tests;

public class TestSelectorTests
{
    [Fact]
    public void SelectTests_UsesDeterministicOrderingAndJobIndex()
    {
        var tests = new[] { "B.Test", "A.Test", "D.Test", "C.Test" };

        var selected = TestSelector.SelectTests(tests, jobNumber: 1, totalJobs: 2);

        Assert.Equal(["A.Test", "C.Test"], selected);
    }

    [Fact]
    public void SelectTests_SelectsSecondJobTests()
    {
        var tests = new[] { "B.Test", "A.Test", "D.Test", "C.Test" };

        var selected = TestSelector.SelectTests(tests, jobNumber: 2, totalJobs: 2);

        Assert.Equal(["B.Test", "D.Test"], selected);
    }

    [Fact]
    public void SelectTests_ReturnsEmpty_WhenNoTestsProvided()
    {
        var selected = TestSelector.SelectTests(Array.Empty<string>(), jobNumber: 1, totalJobs: 3);

        Assert.Empty(selected);
    }
}
