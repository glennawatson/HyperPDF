#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace HyperPdf.Scripts;

/// <summary>
/// Validates every file of a checkout of veraPDF's PDF/A test files with veraPDF, each against its folder's PDF/A level, and records
/// the rules that fail. The PDF/A validator agreement test compares HyperPdf with this record, so CI does not run veraPDF
/// over all of them. Rerun it after changing the veraPDF release or the test file commit in <c>src/verapdf.json</c>.
/// </summary>
/// <remarks>
/// Output: two <c>#</c> header lines naming the veraPDF release and the test file commit, then one tab-separated line per file:
/// the path relative to the checkout with <c>/</c> separators, the level, whether veraPDF found it compliant, the job
/// status, and the failed rules as <c>clause:test</c> joined by <c>;</c>.
/// </remarks>
internal static class RecordVeraPdfResults
{
    /// <summary>The files validated per veraPDF start.</summary>
    private const int BatchSize = 80;

    /// <summary>The longest one batch may take.</summary>
    private const int BatchMinutes = 15;

    /// <summary>The longest a killed process may take to exit.</summary>
    private const int ProcessCleanupSeconds = 30;

    /// <summary>The header lines before the results: the veraPDF release and the test file commit.</summary>
    private const int HeaderLines = 2;

    /// <summary>Gets the folders and the veraPDF flavour each is validated against.</summary>
    private static Dictionary<string, string> Flavours => new(StringComparer.Ordinal)
    {
        ["PDF_A-1a"] = "1a",
        ["PDF_A-1b"] = "1b",
        ["PDF_A-2a"] = "2a",
        ["PDF_A-2b"] = "2b",
        ["PDF_A-2u"] = "2u",
        ["PDF_A-3b"] = "3b",
        ["PDF_A-4"] = "4",
        ["PDF_A-4e"] = "4e",
        ["PDF_A-4f"] = "4f",
    };

    /// <summary>Gets the PDF/UA folders and the veraPDF flavour each is validated against.</summary>
    private static Dictionary<string, string> UaFlavours => new(StringComparer.Ordinal) { ["PDF_UA-1"] = "ua1", ["PDF_UA-2"] = "ua2" };

    /// <summary>Records the results.</summary>
    /// <param name="args">The test file checkout, the veraPDF installation directory, the output file, and optionally <c>ua</c> to record the PDF/UA folders instead of the PDF/A ones.</param>
    /// <returns>A task.</returns>
    /// <exception cref="ArgumentException">The arguments are wrong.</exception>
    internal static async Task Main(string[] args)
    {
        var isUa = args is [_, _, _, "ua"];
        if (args.Length != 3 && !isUa)
        {
            throw new ArgumentException("Provide the test file checkout, the veraPDF installation directory, the output file and optionally 'ua'.");
        }

        var testFilesArgument = args[0];
        var homeArgument = args[1];
        var outputArgument = args[2];
        var flavours = isUa ? UaFlavours : Flavours;

        var testFiles = Path.GetFullPath(testFilesArgument);
        var home = Path.GetFullPath(homeArgument);
        using var setup = new CancellationTokenSource(TimeSpan.FromMinutes(BatchMinutes));

        // The first line of the version banner names the release, such as "veraPDF 1.30.3".
        var release = (await RunAsync(FindJava(), [.. CliArguments(home), "--version"], setup.Token)).Trim().Split('\n')[0].Trim();
        var commit = (await RunAsync("git", ["-C", testFiles, "rev-parse", "HEAD"], setup.Token)).Trim();
        var lines = new List<string> { $"# {release}", $"# veraPDF test files {commit}" };
        foreach (var (folder, flavour) in flavours)
        {
            var files = Directory.GetFiles(Path.Combine(testFiles, folder), "*.pdf", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var batch in files.Chunk(BatchSize))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(BatchMinutes));
                var json = await RunAsync(FindJava(), [.. CliArguments(home), "--format", "json", "--maxfailuresdisplayed", "1", "-f", flavour, .. batch], timeout.Token);
                AddResults(lines, testFiles, flavour, json);
            }

            Console.WriteLine($"{folder}: {files.Length} files");
        }

        await File.WriteAllLinesAsync(Path.GetFullPath(outputArgument), lines, new UTF8Encoding(false), setup.Token);
        Console.WriteLine($"Recorded {lines.Count - HeaderLines} files with {release}.");
    }

    /// <summary>Gets the arguments that start the veraPDF command-line validator.</summary>
    /// <param name="home">The installation directory.</param>
    /// <returns>The Java arguments.</returns>
    private static string[] CliArguments(string home) => ["-cp", Path.Combine(home, "bin", "*"), $"-Dapp.home={home}", "org.verapdf.apps.GreenfieldCliWrapper"];

    /// <summary>Adds a line for each job of a veraPDF JSON report.</summary>
    /// <param name="lines">The lines.</param>
    /// <param name="testFiles">The test file checkout.</param>
    /// <param name="flavour">The flavour validated.</param>
    /// <param name="json">The JSON report.</param>
    private static void AddResults(List<string> lines, string testFiles, string flavour, string json)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var job in document.RootElement.GetProperty("report").GetProperty("jobs").EnumerateArray())
        {
            var path = Path.GetRelativePath(testFiles, job.GetProperty("itemDetails").GetProperty("name").GetString() ?? string.Empty).Replace('\\', '/');
            if (!job.TryGetProperty("validationResult", out var results) || results.GetArrayLength() == 0)
            {
                lines.Add($"{path}\t{flavour}\tFalse\terror\t");
                continue;
            }

            var result = results[0];
            var failed = new List<string>();
            foreach (var rule in result.GetProperty("details").GetProperty("ruleSummaries").EnumerateArray())
            {
                if (rule.GetProperty("ruleStatus").GetString() == "FAILED")
                {
                    failed.Add($"{rule.GetProperty("clause").GetString()}:{rule.GetProperty("testNumber").GetInt32()}");
                }
            }

            failed.Sort(StringComparer.Ordinal);
            lines.Add($"{path}\t{flavour}\t{result.GetProperty("compliant").GetBoolean()}\t{result.GetProperty("jobEndStatus").GetString()}\t{string.Join(';', failed)}");
        }
    }

    /// <summary>Finds Java: <c>JAVA_HOME</c> first, then the path.</summary>
    /// <returns>The executable.</returns>
    private static string FindJava()
    {
        var executable = OperatingSystem.IsWindows() ? "java.exe" : "java";
        var home = Environment.GetEnvironmentVariable("JAVA_HOME");
        var candidate = string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, "bin", executable);
        return candidate is not null && File.Exists(candidate) ? candidate : executable;
    }

    /// <summary>Runs a process to completion and returns its standard output.</summary>
    /// <param name="executable">The program.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">Cancels the run and kills the process.</param>
    /// <returns>The standard output text.</returns>
    /// <exception cref="IOException">The process did not start.</exception>
    private static async Task<string> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new IOException($"{executable} did not start.");

        // Read until the owned process exits so the diagnostics remain available after it is killed. veraPDF exits
        // non-zero when a file is not compliant, so the exit code is not a failure here; the JSON report is checked.
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            var status = await process.WaitForExitStatusAsync(cancellationToken);
            var text = await output;
            return status.Canceled || status.Signal is not null || text.Length == 0
                ? throw new IOException($"{Path.GetFileName(executable)} gave no output: {await error}")
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
