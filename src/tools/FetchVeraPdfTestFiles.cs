#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;

namespace HyperPdf.Scripts;

/// <summary>
/// Checks out the PDF/A folders of veraPDF's PDF/A test files (CC BY 4.0) at the commit <c>src/verapdf.json</c> pins, for the
/// test that compares HyperPdf's PDF/A validator with veraPDF's recorded results. It uses a sparse, blob-less git clone,
/// so only the PDF/A files are downloaded, and reuses a checkout already at the pinned commit.
/// </summary>
internal static class FetchVeraPdfTestFiles
{
    /// <summary>The file in the repository root that pins the commit.</summary>
    private const string VersionFile = "src/verapdf.json";

    /// <summary>The repository of veraPDF's test files.</summary>
    private const string Repository = "https://github.com/veraPDF/veraPDF-corpus.git";

    /// <summary>The length of a full commit hash.</summary>
    private const int CommitLength = 40;

    /// <summary>The longest the checkout may take.</summary>
    private const int FetchMinutes = 15;

    /// <summary>The longest a killed process may take to exit.</summary>
    private const int ProcessCleanupSeconds = 30;

    /// <summary>Gets the folders checked out: one per PDF/A level, then one per PDF/UA part.</summary>
    private static string[] Folders => ["PDF_A-1a", "PDF_A-1b", "PDF_A-2a", "PDF_A-2b", "PDF_A-2u", "PDF_A-3b", "PDF_A-4", "PDF_A-4e", "PDF_A-4f", "PDF_UA-1", "PDF_UA-2"];

    /// <summary>Checks out the test files into a folder, or reuses the checkout already there.</summary>
    /// <param name="args">The checkout folder.</param>
    /// <returns>A task.</returns>
    /// <exception cref="ArgumentException">No folder was given.</exception>
    /// <exception cref="IOException">The folder holds something else.</exception>
    internal static async Task Main(string[] args)
    {
        if (args is not [var destinationArgument])
        {
            throw new ArgumentException("Provide the folder for veraPDF's PDF/A test files.");
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(FetchMinutes));
        var commit = await ReadCommitAsync(timeout.Token);
        var folder = Path.GetFullPath(destinationArgument);
        if (!await IsCheckedOutAsync(folder, commit, timeout.Token))
        {
            if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length > 0)
            {
                throw new IOException($"The folder must be empty or hold veraPDF's PDF/A test files at {commit}; remove it or choose another: {folder}");
            }

            _ = await RunAsync(["clone", "--quiet", "--filter=blob:none", "--no-checkout", "--sparse", Repository, folder], timeout.Token);
            _ = await RunAsync(["-C", folder, "sparse-checkout", "set", .. Folders], timeout.Token);
            _ = await RunAsync(["-C", folder, "checkout", "--quiet", "--detach", commit], timeout.Token);
        }
        else
        {
            // A checkout made before a folder was added still gets the new folder.
            _ = await RunAsync(["-C", folder, "sparse-checkout", "set", .. Folders], timeout.Token);
        }

        var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (!string.IsNullOrWhiteSpace(githubOutput))
        {
            await File.AppendAllLinesAsync(githubOutput, [$"test-files-dir={folder}"], timeout.Token);
        }

        Console.WriteLine($"veraPDF PDF/A test files {commit}: {folder}");
        Console.WriteLine($"Set HYPERPDF_VERAPDF_TEST_FILES={folder} to run the PDF/A validator agreement test.");
    }

    /// <summary>Reads the pinned commit from the version file, looking up from the working folder.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The commit.</returns>
    /// <exception cref="InvalidDataException">The file does not pin a full commit hash.</exception>
    private static async Task<string> ReadCommitAsync(CancellationToken cancellationToken)
    {
        var path = FindVersionFile();
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken);
        var commit = document.RootElement.GetProperty("testFilesCommit").GetString() ?? string.Empty;

        // Only a full hash is used, as it is passed to git.
        return commit.Length == CommitLength && !commit.AsSpan().ContainsAnyExcept("0123456789abcdef")
            ? commit
            : throw new InvalidDataException($"{path} must pin testFilesCommit to a full lower-case commit hash, not '{commit}'.");
    }

    /// <summary>Finds the version file in the working folder or a parent.</summary>
    /// <returns>Its path.</returns>
    /// <exception cref="FileNotFoundException">No folder has it.</exception>
    private static string FindVersionFile()
    {
        for (var folder = new DirectoryInfo(Environment.CurrentDirectory); folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName, VersionFile);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"No {VersionFile} in the working folder or its parents.", VersionFile);
    }

    /// <summary>Determines whether a folder is already a checkout at the commit, so local runs reuse it.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static async Task<bool> IsCheckedOutAsync(string folder, string commit, CancellationToken cancellationToken) =>
        Directory.Exists(Path.Combine(folder, ".git"))
        && string.Equals((await RunAsync(["-C", folder, "rev-parse", "HEAD"], cancellationToken)).Trim(), commit, StringComparison.Ordinal);

    /// <summary>Runs git to completion and returns its output.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">Cancels the run and kills the process.</param>
    /// <returns>The standard output text.</returns>
    /// <exception cref="IOException">git did not start or did not succeed.</exception>
    private static async Task<string> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new IOException("git did not start.");

        // Read until the owned process exits so the diagnostics remain available after it is killed.
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            var status = await process.WaitForExitStatusAsync(cancellationToken);
            var text = await output;
            return status.Canceled || status.Signal is not null || status.ExitCode != 0
                ? throw new IOException($"git {string.Join(' ', arguments)} failed: {text}{await error}")
                : text;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(ProcessCleanupSeconds));
            _ = await process.WaitForExitStatusAsync(cleanup.Token);
            await Console.Error.WriteLineAsync((await output + await error).AsMemory(), cleanup.Token);
            throw;
        }
    }
}
