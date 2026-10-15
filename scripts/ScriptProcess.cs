// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Scripts;

/// <summary>Runs repository tools with argument boundaries and cancellation preserved.</summary>
internal static class ScriptProcess
{
    /// <summary>The .NET command available on PATH.</summary>
    internal const string Dotnet = "dotnet";

    /// <summary>Finds the checkout from its source path, CI workspace or working directory.</summary>
    /// <param name="source">The calling script's source path.</param>
    /// <returns>The checkout directory.</returns>
    /// <exception cref="DirectoryNotFoundException">The checkout cannot be located.</exception>
    internal static string Root([CallerFilePath] string source = "")
    {
        var sourceRoot = Path.GetDirectoryName(Path.GetDirectoryName(source));
        if (sourceRoot is not null && IsCheckout(sourceRoot))
        {
            return sourceRoot;
        }

        // CI source paths may be mapped to /_/, which is not the runner's checkout.
        if (Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") is { Length: > 0 } workspace && IsCheckout(workspace))
        {
            return Path.GetFullPath(workspace);
        }

        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (IsCheckout(directory.FullName))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Cannot locate the checkout containing HyperPdf.slnx from the source path, GITHUB_WORKSPACE or working directory.");
    }

    /// <summary>Starts a child with explicit arguments and environment overrides.</summary>
    /// <param name="executable">The executable path or command.</param>
    /// <param name="arguments">The separate arguments.</param>
    /// <param name="directory">The working directory.</param>
    /// <param name="environment">Environment overrides.</param>
    /// <param name="capture">Whether to capture output.</param>
    /// <returns>The running child.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <c>Process.Start(info)</c> is <see langword="null"/>.</exception>
    internal static Process Start(
        string executable,
        IEnumerable<string> arguments,
        string directory,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool capture = false)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = capture, RedirectStandardError = capture };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var entry in environment)
            {
                info.Environment[entry.Key] = entry.Value;
            }
        }

        return Process.Start(info) ?? throw new InvalidOperationException($"Cannot start {executable}.");
    }

    /// <summary>Runs a child and propagates its status, killing its tree on cancellation.</summary>
    /// <param name="executable">The executable path or command.</param>
    /// <param name="arguments">The separate arguments.</param>
    /// <param name="directory">The working directory.</param>
    /// <param name="cancellationToken">Cancels the child process.</param>
    /// <param name="environment">Environment overrides.</param>
    /// <returns>The exit code, or one when terminated by a signal.</returns>
    internal static async Task<int> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string directory,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        using var process = Start(executable, arguments, directory, environment);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }

        var status = await process.WaitForExitStatusAsync(cancellationToken);
        return status is { Signal: null, ExitCode: int code } ? code : 1;
    }

    /// <summary>Captures a child's output and reports a failed child with its diagnostic output.</summary>
    /// <param name="executable">The executable path or command.</param>
    /// <param name="arguments">The separate arguments.</param>
    /// <param name="directory">The working directory.</param>
    /// <param name="cancellationToken">Cancels the child process.</param>
    /// <param name="environment">Environment overrides.</param>
    /// <returns>The standard output.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <c>status is not { Signal: null, ExitCode: 0 }</c>.</exception>
    internal static async Task<string> CaptureAsync(
        string executable,
        IEnumerable<string> arguments,
        string directory,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        using var process = Start(executable, arguments, directory, environment, true);
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }

        var result = await output;
        var errors = await error;
        var status = await process.WaitForExitStatusAsync(cancellationToken);
        if (status is not { Signal: null, ExitCode: 0 })
        {
            throw new InvalidOperationException($"{executable} exited {status}: {errors}");
        }

        return result;
    }

    /// <summary>Checks whether a directory contains the repository's solution.</summary>
    /// <param name="directory">The candidate checkout directory.</param>
    /// <returns>Whether the solution is present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCheckout(string directory) => File.Exists(Path.Combine(directory, "HyperPdf.slnx"));
}
