namespace Meziantou.ShardedTest;

internal sealed record TestListResult(TestListFormat Format, IReadOnlyList<string> Tests, int AssemblyCount)
{
    public static TestListResult Unknown { get; } = new(TestListFormat.Unknown, [], AssemblyCount: 0);
}
