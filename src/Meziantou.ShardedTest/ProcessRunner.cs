using System.Diagnostics;
using System.Text;

namespace Meziantou.ShardedTest;

internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool forwardOutput = false)
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
}
