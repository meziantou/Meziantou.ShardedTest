namespace Meziantou.ShardedTest;

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
