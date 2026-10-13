// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Runs a command-line tool found on the path, such as openssl or pdfsig, for checks that do not use the library.</summary>
internal static class ExternalTool
{
    /// <summary>The longest a tool may run.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    /// <summary>Runs a tool and collects what it prints.</summary>
    /// <param name="tool">The tool's name, found on the path.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The exit code and output, or <see langword="null"/> when the tool is not installed.</returns>
    internal static async Task<ToolResult?> RunAsync(string tool, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(tool) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Timeout);
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync(limit.Token);
            var errors = process.StandardError.ReadToEndAsync(limit.Token);
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            return new(await output.ConfigureAwait(false) + await errors.ConfigureAwait(false));
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    /// <summary>Makes a folder of its own for a test's files.</summary>
    /// <returns>The folder, which the caller deletes.</returns>
    internal static string MakeFolder() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"hyperpdf-signing-{Guid.NewGuid():N}")).FullName;

    /// <summary>What a tool printed and how it ended.</summary>
    /// <param name="Output">Standard output and standard error together.</param>
    internal readonly record struct ToolResult(string Output);
}
