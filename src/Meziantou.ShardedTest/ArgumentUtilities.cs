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

    public static bool HasListTestsArg(string[] args)
    {
        return args.Any(arg => arg.Equals("--list-tests", StringComparison.OrdinalIgnoreCase));
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
