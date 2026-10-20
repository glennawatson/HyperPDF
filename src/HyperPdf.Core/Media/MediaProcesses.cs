// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace HyperPdf.Core.Media;

/// <summary>Starts, reads and stops the FFmpeg programs.</summary>
internal static class MediaProcesses
{
    /// <summary>The longest text read from a program, in characters, so a faulty program cannot fill memory.</summary>
    private const int MaxTextLength = 4_000_000;

    /// <summary>The characters read at a time from a program's output.</summary>
    private const int ReadChunk = 4096;

    /// <summary>Starts a program with its output and error output redirected.</summary>
    /// <param name="program">The program's path.</param>
    /// <param name="arguments">The program's arguments.</param>
    /// <returns>The running process, or <see langword="null"/> when it could not be started.</returns>
    internal static Process? Start(string program, string[] arguments)
    {
        var start = new ProcessStartInfo(program)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            return Process.Start(start);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The program is gone or cannot be run; the caller reports that the media cannot play.
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Runs a program to its end and reads what it prints.</summary>
    /// <param name="program">The program's path.</param>
    /// <param name="arguments">The program's arguments.</param>
    /// <param name="cancellationToken">Stops the program.</param>
    /// <returns>The text the program printed, which is empty when the program failed, or <see langword="null"/> when it could not run.</returns>
    internal static async Task<string?> RunAsync(string program, string[] arguments, CancellationToken cancellationToken)
    {
        using var process = Start(program, arguments);
        if (process is null)
        {
            return null;
        }

        try
        {
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            var text = await ReadLimitedAsync(process.StandardOutput, cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            _ = await errors.ConfigureAwait(false);
            return text;
        }
        catch (OperationCanceledException)
        {
            Stop(process);
            throw;
        }
    }

    /// <summary>Reads and drops what a program prints on its error output, so the program never waits for it to be read.</summary>
    /// <param name="errors">The program's error output.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    /// <returns>A task that completes when the output ends.</returns>
    internal static async Task DrainAsync(StreamReader errors, CancellationToken cancellationToken)
    {
        try
        {
            _ = await errors.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The run ended.
        }
        catch (IOException)
        {
            // The program closed its output while it was being stopped.
        }
    }

    /// <summary>Stops a process and everything it started, if it is still running.</summary>
    /// <param name="process">The process.</param>
    internal static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process ended between the check and the kill, which is what was wanted.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The operating system refused to stop it; it is dropped and ends on its own when its output is closed.
        }
    }

    /// <summary>Reads text up to the limit.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    /// <returns>The text; anything past the limit is left out.</returns>
    private static async Task<string> ReadLimitedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        var buffer = new char[ReadChunk];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (text.Length < MaxTextLength)
            {
                _ = text.Append(buffer, 0, Math.Min(read, MaxTextLength - text.Length));
            }
        }

        return text.ToString();
    }
}
