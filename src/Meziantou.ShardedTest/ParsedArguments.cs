namespace Meziantou.ShardedTest;

internal sealed record ParsedArguments(int ShardIndex, int TotalShards, string[] ForwardArgs);
