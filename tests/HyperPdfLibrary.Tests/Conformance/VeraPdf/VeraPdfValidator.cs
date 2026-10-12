// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Runs the veraPDF command-line validator, installed by <c>scripts/InstallVeraPdf.cs</c>, as a separate Java process.
/// veraPDF is the independent reference for PDF/A; the tests never link it, so its GPL/MPL licence does not reach the
/// library. Without an installation the tests skip, unless <see cref="RequireVariable"/> is set, as it is in CI.
/// </summary>
internal static class VeraPdfValidator
{
    /// <summary>The variable naming the veraPDF installation directory.</summary>
    internal const string HomeVariable = "PDFVIEWERLITE_VERAPDF";

    /// <summary>The variable that turns a missing installation into a failure.</summary>
    internal const string RequireVariable = "PDFVIEWERLITE_REQUIRE_VERAPDF";

    /// <summary>The flavour that makes veraPDF pick the profile from the file's own claim.</summary>
    internal const string DetectFlavour = "0";

    /// <summary>The features read back to compare with HyperPdf's reading.</summary>
    private const string Features = "informationDict,page,outputIntent";

    /// <summary>The longest a validation may run.</summary>
    private const int TimeoutMinutes = 10;

    /// <summary>The longest a killed process may take to exit.</summary>
    private const int CleanupSeconds = 30;

    /// <summary>Gets the veraPDF installation directory, or skips the test when there is none.</summary>
    /// <returns>The directory holding <c>bin/cli-*.jar</c>.</returns>
    /// <exception cref="InvalidOperationException">veraPDF is required but missing.</exception>
    internal static string RequireHome()
    {
        var home = Environment.GetEnvironmentVariable(HomeVariable);
        if (home is { Length: > 0 } && Directory.Exists(Path.Combine(home, "bin")))
        {
            return home;
        }

        const string message = $"veraPDF is not installed. Run 'dotnet run --file scripts/InstallVeraPdf.cs -- <dir>' and set {HomeVariable} to that directory.";
        if (Environment.GetEnvironmentVariable(RequireVariable) == "1")
        {
            throw new InvalidOperationException(message);
        }

        Skip.Test(message);
        return string.Empty;
    }

    /// <summary>Validates files against one flavour and reads the features veraPDF extracts.</summary>
    /// <param name="home">The installation directory.</param>
    /// <param name="flavour">The flavour, such as 1b, 4 or <see cref="DetectFlavour"/>.</param>
    /// <param name="files">The files.</param>
    /// <param name="cancellationToken">Cancels the run and kills the process.</param>
    /// <returns>The result for each file, by full path.</returns>
    /// <exception cref="InvalidOperationException">Java could not start veraPDF.</exception>
    /// <exception cref="InvalidDataException">veraPDF gave no result for a file.</exception>
    internal static async Task<Dictionary<string, VeraPdfJob>> ValidateAsync(string home, string flavour, IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(TimeoutMinutes));
        var start = CreateStart(home, flavour, files);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("veraPDF did not start.");
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(true);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
            await process.WaitForExitAsync(cleanup.Token);
            throw;
        }

        // veraPDF reports non-conforming files in the XML and signals them in its exit code, so the report is the result.
        var report = await output;
        var jobs = VeraPdfReportReader.Read(Encoding.UTF8.GetBytes(report));
        foreach (var file in files)
        {
            if (!jobs.ContainsKey(Path.GetFullPath(file)))
            {
                throw new InvalidDataException($"veraPDF gave no result for {file}. {await error}");
            }
        }

        return jobs;
    }

    /// <summary>Builds the Java command line the veraPDF launcher scripts would run.</summary>
    /// <param name="home">The installation directory.</param>
    /// <param name="flavour">The flavour.</param>
    /// <param name="files">The files.</param>
    /// <returns>The start information.</returns>
    private static ProcessStartInfo CreateStart(string home, string flavour, IReadOnlyList<string> files)
    {
        var start = new ProcessStartInfo(FindJava()) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
        string[] arguments =
        [
            "-cp", Path.Combine(home, "bin", "*"), "-Dfile.encoding=UTF8", $"-Dapp.home={home}", $"-Dbasedir={home}",
            "--add-exports=java.base/sun.security.pkcs=ALL-UNNAMED", "org.verapdf.apps.GreenfieldCliWrapper",
            "--format", "xml", "--maxfailuresdisplayed", "3", "-x", Features, "-f", flavour,
        ];
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var file in files)
        {
            start.ArgumentList.Add(Path.GetFullPath(file));
        }

        return start;
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
}
