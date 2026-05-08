namespace Meziantou.ShardedTest;

internal sealed record ParsedArguments(int ShardIndex, int TotalShards, bool Verbose, string[] ForwardArgs);
