using System;
using System.Collections.Generic;
using System.Threading;
using CliWrap;
using CliWrap.Buffered;

namespace MacBundle;

internal static class CommandRunner
{
    public static string? TryGetStandardOutput(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout
    )
    {
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(timeout);

            var result = Cli
                .Wrap(fileName)
                .WithArguments(arguments)
                .WithWorkingDirectory(workingDirectory)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationTokenSource.Token)
                .GetAwaiter()
                .GetResult();

            var output = result.StandardOutput.Trim();
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;

            return output;
        }
        catch
        {
            return null;
        }
    }
}
