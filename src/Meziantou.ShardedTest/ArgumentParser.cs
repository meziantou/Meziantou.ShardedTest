using System.Globalization;

namespace Meziantou.ShardedTest;

internal static class ArgumentParser
{
    public static bool TryParse(string[] args, out ParsedArguments parsed, out string error)
    {
        parsed = null!;
        error = string.Empty;

        int? shardIndex = null;
        int? totalShards = null;
        var forwardArgs = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (TryReadOption(args, ref i, "--shard-index", out var shardIndexValue, out error))
            {
                if (error.Length > 0)
                {
                    return false;
                }

                if (shardIndex.HasValue)
                {
                    error = "The --shard-index option is specified more than once.";
                    return false;
                }

                if (!int.TryParse(shardIndexValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedShardIndex))
                {
                    error = "The --shard-index option must be an integer.";
                    return false;
                }

                shardIndex = parsedShardIndex;
                continue;
            }

            if (TryReadOption(args, ref i, "--total-shards", out var totalShardsValue, out error))
            {
                if (error.Length > 0)
                {
                    return false;
                }

                if (totalShards.HasValue)
                {
                    error = "The --total-shards option is specified more than once.";
                    return false;
                }

                if (!int.TryParse(totalShardsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTotalShards))
                {
                    error = "The --total-shards option must be an integer.";
                    return false;
                }

                totalShards = parsedTotalShards;
                continue;
            }

            forwardArgs.Add(arg);
        }

        if (!shardIndex.HasValue)
        {
            var envValue = Environment.GetEnvironmentVariable("CI_NODE_INDEX");
            if (envValue is not null)
            {
                if (!int.TryParse(envValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedEnvShardIndex))
                {
                    error = "The CI_NODE_INDEX environment variable must be an integer.";
                    return false;
                }

                shardIndex = parsedEnvShardIndex;
            }
        }

        if (!totalShards.HasValue)
        {
            var envValue = Environment.GetEnvironmentVariable("CI_NODE_TOTAL");
            if (envValue is not null)
            {
                if (!int.TryParse(envValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedEnvTotalShards))
                {
                    error = "The CI_NODE_TOTAL environment variable must be an integer.";
                    return false;
                }

                totalShards = parsedEnvTotalShards;
            }
        }

        if (!shardIndex.HasValue || !totalShards.HasValue)
        {
            error = "Both --shard-index and --total-shards must be specified.";
            return false;
        }

        if (totalShards.Value <= 0)
        {
            error = "--total-shards must be greater than zero.";
            return false;
        }

        if (shardIndex.Value <= 0)
        {
            error = "--shard-index must be greater than zero.";
            return false;
        }

        if (shardIndex.Value > totalShards.Value)
        {
            error = "--shard-index must be less than or equal to --total-shards.";
            return false;
        }

        parsed = new ParsedArguments(shardIndex.Value, totalShards.Value, forwardArgs.ToArray());
        return true;
    }

    private static bool TryReadOption(string[] args, ref int index, string optionName, out string value, out string error)
    {
        error = string.Empty;
        value = string.Empty;
        var arg = args[index];

        if (arg.Equals(optionName, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                error = $"Missing value for {optionName}.";
                return true;
            }

            value = args[++index];
            return true;
        }

        var prefix = optionName + "=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = arg[prefix.Length..];
            if (value.Length == 0)
            {
                error = $"Missing value for {optionName}.";
            }

            return true;
        }

        return false;
    }
}
