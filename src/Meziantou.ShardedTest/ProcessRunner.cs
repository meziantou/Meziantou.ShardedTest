using System.Diagnostics;
using System.Text;

namespace Meziantou.ShardedTest;

internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        bool forwardOutput = false,
        bool verbose = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environmentVariables is not null)
        {
            foreach (var (name, value) in environmentVariables)
            {
                startInfo.Environment[name] = value;
            }
        }

        if (verbose && IsDotnetCommand(fileName))
        {
            Console.Error.WriteLine($"Executing: {BuildCommandLine(fileName, arguments)}");
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var outputTask = ReadStreamAsync(process.StandardOutput, outputBuilder, forwardOutput ? Console.Out : null, cancellationToken);
        var errorTask = ReadStreamAsync(process.StandardError, errorBuilder, forwardOutput ? Console.Error : null, cancellationToken);

        await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(cancellationToken));

        return new ProcessResult(process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }

    private static async Task ReadStreamAsync(StreamReader reader, StringBuilder builder, TextWriter? output, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        while (true)
        {
            var readCount = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (readCount == 0)
            {
                return;
            }

            builder.Append(buffer, 0, readCount);
            if (output is not null)
            {
                await output.WriteAsync(buffer.AsMemory(0, readCount), cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
        }
    }

    private static bool IsDotnetCommand(string fileName)
    {
        var commandName = Path.GetFileNameWithoutExtension(fileName);
        return commandName.Equals("dotnet", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCommandLine(string fileName, IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder(EscapeArgument(fileName));
        foreach (var argument in arguments)
        {
            builder.Append(' ');
            builder.Append(EscapeArgument(argument));
        }

        return builder.ToString();
    }

    private static string EscapeArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        if (argument.IndexOfAny([' ', '\t', '\r', '\n', '"']) < 0)
        {
            return argument;
        }

        var escaped = argument
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return "\"" + escaped + "\"";
    }
}
