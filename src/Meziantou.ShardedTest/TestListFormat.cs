namespace Meziantou.ShardedTest;

/// <summary>
/// Describes the runner that produced the output of <c>dotnet test --list-tests</c>.
/// </summary>
internal enum TestListFormat
{
    Unknown,
    VSTest,
    MicrosoftTestingPlatform,
}
