namespace Meziantou.ShardedTest.Tests;

public class TestFilterBuilderTests
{
    [Fact]
    public void BuildFilters_JoinsTestsWithOrExpression()
    {
        var allTests = new[] { "Alpha.ClassA.Test1", "Beta.ClassB.Test1" };

        var filters = TestFilterBuilder.BuildFilters(allTests, allTests, maxFilterLength: 200);

        Assert.Single(filters);
        Assert.Equal(
            "FullyQualifiedName=Alpha.ClassA.Test1|FullyQualifiedName=Beta.ClassB.Test1",
            filters[0]);
    }

    [Fact]
    public void BuildFilters_CompressesToClassPrefix_WhenAllClassTestsSelected()
    {
        var allTests = new[]
        {
            "Contoso.App.Tests.ClassA.Test1",
            "Contoso.App.Tests.ClassA.Test2",
            "Contoso.App.Tests.ClassB.Test1",
        };

        var selected = new[]
        {
            "Contoso.App.Tests.ClassA.Test1",
            "Contoso.App.Tests.ClassA.Test2",
        };

        var filters = TestFilterBuilder.BuildFilters(allTests, selected, maxFilterLength: 200);

        Assert.Single(filters);
        Assert.Equal("FullyQualifiedName~Contoso.App.Tests.ClassA.", filters[0]);
    }

    [Fact]
    public void BuildFilters_SplitsIntoMultipleFilters_WhenMaxLengthExceeded()
    {
        var allTests = new[]
        {
            "Alpha.ClassA.Test1",
            "Beta.ClassB.Test1",
            "Gamma.ClassC.Test1",
        };

        var filters = TestFilterBuilder.BuildFilters(allTests, allTests, maxFilterLength: 50);

        Assert.True(filters.Count > 1);
        Assert.All(filters, filter => Assert.True(filter.Length <= 50));
    }

    [Fact]
    public void BuildFilters_SplitsLargeSelections_WithoutDroppingTests()
    {
        var allTests = Enumerable.Range(1, 20)
            .Select(index => $"Namespace{index}.Class{index}.Test{index}")
            .ToArray();

        var filters = TestFilterBuilder.BuildFilters(allTests, allTests, maxFilterLength: 80);

        Assert.True(filters.Count > 1);
        Assert.All(filters, filter => Assert.True(filter.Length <= 80));

        var parts = filters
            .SelectMany(filter => filter.Split('|', StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        Assert.All(parts, part => Assert.StartsWith("FullyQualifiedName=", part, StringComparison.Ordinal));

        var names = parts
            .Select(part => part["FullyQualifiedName=".Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(allTests.Order(StringComparer.Ordinal), names);
    }
}
