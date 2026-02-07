using System.Globalization;

namespace Meziantou.ShardedTest;

internal static class ArgumentParser
{
    public static bool TryParse(string[] args, out ParsedArguments parsed, out string error)
    {
        parsed = null!;
        error = string.Empty;

        int? jobNumber = null;
        int? totalJobs = null;
        var forwardArgs = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (TryReadOption(args, ref i, "--job-number", out var jobNumberValue, out error))
            {
                if (error.Length > 0)
                {
                    return false;
                }

                if (jobNumber.HasValue)
                {
                    error = "The --job-number option is specified more than once.";
                    return false;
                }

                if (!int.TryParse(jobNumberValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedJobNumber))
                {
                    error = "The --job-number option must be an integer.";
                    return false;
                }

                jobNumber = parsedJobNumber;
                continue;
            }

            if (TryReadOption(args, ref i, "--total-jobs", out var totalJobsValue, out error))
            {
                if (error.Length > 0)
                {
                    return false;
                }

                if (totalJobs.HasValue)
                {
                    error = "The --total-jobs option is specified more than once.";
                    return false;
                }

                if (!int.TryParse(totalJobsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTotalJobs))
                {
                    error = "The --total-jobs option must be an integer.";
                    return false;
                }

                totalJobs = parsedTotalJobs;
                continue;
            }

            forwardArgs.Add(arg);
        }

        if (!jobNumber.HasValue || !totalJobs.HasValue)
        {
            error = "Both --job-number and --total-jobs must be specified.";
            return false;
        }

        if (totalJobs.Value <= 0)
        {
            error = "--total-jobs must be greater than zero.";
            return false;
        }

        if (jobNumber.Value <= 0)
        {
            error = "--job-number must be greater than zero.";
            return false;
        }

        if (jobNumber.Value > totalJobs.Value)
        {
            error = "--job-number must be less than or equal to --total-jobs.";
            return false;
        }

        parsed = new ParsedArguments(jobNumber.Value, totalJobs.Value, forwardArgs.ToArray());
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
