namespace Meziantou.ShardedTest;

internal static class ArgumentUtilities
{
    public static string[] RemoveFilterArgs(string[] args)
    {
        var result = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (IsOption(arg, "--filter", out var requiresValue))
            {
                if (requiresValue && i + 1 < args.Length)
                {
                    i++;
                }

                continue;
            }

            if (arg.Equals("--list-tests", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(arg);
        }

        return result.ToArray();
    }

    public static string[] RemoveListTestsArgs(string[] args)
    {
        return args.Where(arg => !arg.Equals("--list-tests", StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    /// <summary>
    /// Removes the options that are only valid when the tests are run. Microsoft.Testing.Platform rejects the
    /// command line (exit code 5) when an extension such as the TRX report or the code coverage is enabled
    /// while listing the tests.
    /// </summary>
    public static string[] RemoveRunOnlyArgs(string[] args)
    {
        var result = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var name = GetOptionName(arg, out var hasInlineValue);
            if (!IsRunOnlyOption(name, out var requiresValue))
            {
                result.Add(arg);
                continue;
            }

            if (requiresValue && !hasInlineValue && i + 1 < args.Length && !args[i + 1].StartsWith('-'))
            {
                i++;
            }
        }

        return result.ToArray();
    }

    public static bool HasListTestsArg(string[] args)
    {
        return args.Any(arg => arg.Equals("--list-tests", StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasArg(IReadOnlyList<string> args, string optionName)
    {
        return args.Any(arg => IsOption(arg, optionName, out _));
    }

    private static bool IsRunOnlyOption(string name, out bool requiresValue)
    {
        // Extensions follow the same naming convention: "--extension" enables it and "--extension-xxx" configures it
        if (name.StartsWith("--report-", StringComparison.OrdinalIgnoreCase))
        {
            requiresValue = name.EndsWith("-filename", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        if (name.Equals("--coverage", StringComparison.OrdinalIgnoreCase)
            || name.Equals("--crashdump", StringComparison.OrdinalIgnoreCase)
            || name.Equals("--hangdump", StringComparison.OrdinalIgnoreCase))
        {
            requiresValue = false;
            return true;
        }

        if (name.StartsWith("--coverage-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("--crashdump-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("--hangdump-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("--retry-failed-tests", StringComparison.OrdinalIgnoreCase))
        {
            requiresValue = true;
            return true;
        }

        requiresValue = false;
        return false;
    }

    private static string GetOptionName(string arg, out bool hasInlineValue)
    {
        var index = arg.IndexOf('=', StringComparison.Ordinal);
        hasInlineValue = index >= 0;
        return hasInlineValue ? arg[..index] : arg;
    }

    private static bool IsOption(string arg, string optionName, out bool requiresValue)
    {
        requiresValue = false;

        if (arg.Equals(optionName, StringComparison.OrdinalIgnoreCase))
        {
            requiresValue = true;
            return true;
        }

        var prefix = optionName + "=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
