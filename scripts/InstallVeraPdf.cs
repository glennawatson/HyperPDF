#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace HyperPdf.Scripts;

/// <summary>
/// Installs the veraPDF command-line validator that the PDF/A output tests run as a separate process. veraPDF is
/// dual-licensed GPLv3+ and MPLv2+; it is downloaded and run, never linked or redistributed. It needs Java 11 or later.
/// The release comes from <c>verapdf.json</c> in the repository root, which Renovate keeps current.
/// </summary>
/// <remarks>
/// veraPDF publishes PGP signatures but no digests, so the archive is fetched over HTTPS from the veraPDF site, only the
/// installer entry named for the release is used, the installed CLI must report the same release, and the archive's
/// SHA-256 is printed so each run records what it installed.
/// </remarks>
internal static class InstallVeraPdf
{
    /// <summary>The file in the repository root that names the release.</summary>
    private const string VersionFile = "verapdf.json";

    /// <summary>The parts of a release number such as 1.30.3.</summary>
    private const int ReleaseParts = 3;

    /// <summary>The largest archive accepted.</summary>
    private const long MaximumArchiveBytes = 128L * 1024 * 1024;

    /// <summary>The download buffer size.</summary>
    private const int BufferSize = 65_536;

    /// <summary>The longest the whole setup may take.</summary>
    private const int SetupMinutes = 10;

    /// <summary>The number of installer packs installed: the GUI pack, which carries the CLI jar, and the scripts.</summary>
    private const int SelectedPacks = 2;

    /// <summary>The longest a killed process may take to exit.</summary>
    private const int ProcessCleanupSeconds = 30;

    /// <summary>The client for the one download.</summary>
    private static readonly HttpClient Client = new();

    /// <summary>Gets the installer packs, in the installer's order.</summary>
    private static string[] Packs => ["veraPDF GUI", "veraPDF Mac and *nix Scripts", "veraPDF Validation model", "veraPDF Documentation", "veraPDF Sample Plugins"];

    /// <summary>Installs the release into a folder, or reuses the release already there, and checks it.</summary>
    /// <param name="args">The installation folder.</param>
    /// <returns>A task.</returns>
    /// <exception cref="ArgumentException">No folder was given.</exception>
    internal static async Task Main(string[] args)
    {
        if (args is not [var destinationArgument])
        {
            throw new ArgumentException("Provide the veraPDF installation directory.");
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(SetupMinutes));
        var version = await ReadVersionAsync(timeout.Token);
        var home = Path.GetFullPath(destinationArgument);
        if (!IsInstalled(home, version))
        {
            await InstallAsync(home, version, timeout.Token);
        }

        await ReportAsync(home, version, timeout.Token);
    }

    /// <summary>Reads the release from the version file, looking up from the working folder.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The release number.</returns>
    /// <exception cref="InvalidDataException">The file does not name a release number.</exception>
    private static async Task<string> ReadVersionAsync(CancellationToken cancellationToken)
    {
        var path = FindVersionFile();
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken);
        var version = document.RootElement.GetProperty("version").GetString() ?? string.Empty;

        // Only a release number is used, as it becomes part of the download URL and file names.
        return IsReleaseNumber(version)
            ? version
            : throw new InvalidDataException($"{path} must name a veraPDF release such as 1.30.3, not '{version}'.");
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

    /// <summary>Determines whether text is a release number: three dot-separated runs of digits.</summary>
    /// <param name="version">The text.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsReleaseNumber(string version)
    {
        var parts = 1;
        var digits = 0;
        foreach (var character in version)
        {
            if (character == '.' && digits > 0)
            {
                parts++;
                digits = 0;
            }
            else if (char.IsAsciiDigit(character))
            {
                digits++;
            }
            else
            {
                return false;
            }
        }

        return parts == ReleaseParts && digits > 0;
    }

    /// <summary>Determines whether a folder already holds the release, so local runs reuse it.</summary>
    /// <param name="home">The folder.</param>
    /// <param name="version">The release.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    /// <exception cref="IOException">The folder holds something else.</exception>
    private static bool IsInstalled(string home, string version)
    {
        if (File.Exists(CliJar(home, version)))
        {
            return true;
        }

        return Directory.Exists(home) && Directory.GetFileSystemEntries(home).Length > 0
            ? throw new IOException($"The veraPDF installation directory must be empty or hold veraPDF {version}; remove it or choose another: {home}")
            : false;
    }

    /// <summary>Downloads and installs a release.</summary>
    /// <param name="home">The empty installation folder.</param>
    /// <param name="version">The release.</param>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <returns>A task.</returns>
    /// <exception cref="FileNotFoundException">The installer did not write the validator.</exception>
    private static async Task InstallAsync(string home, string version, CancellationToken cancellationToken)
    {
        var work = Path.Combine(Path.GetTempPath(), $"verapdf-setup-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(work);
        try
        {
            var installer = await FetchInstallerAsync(version, work, cancellationToken);
            var answers = Path.Combine(work, "auto-install.xml");
            WriteAnswers(answers, home);
            _ = await RunAsync(FindJava(), ["-jar", installer, answers], cancellationToken);
            if (!File.Exists(CliJar(home, version)))
            {
                throw new FileNotFoundException("The veraPDF installer did not write the command-line validator.", CliJar(home, version));
            }
        }
        finally
        {
            Directory.Delete(work, true);
        }
    }

    /// <summary>Downloads a release's installer archive and extracts the installer jar.</summary>
    /// <param name="version">The release.</param>
    /// <param name="work">The scratch folder.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The installer jar.</returns>
    /// <exception cref="InvalidDataException">The archive has no installer for the release.</exception>
    private static async Task<string> FetchInstallerAsync(string version, string work, CancellationToken cancellationToken)
    {
        var url = new Uri($"https://software.verapdf.org/rel/{version[..version.LastIndexOf('.')]}/verapdf-greenfield-{version}-installer.zip");
        var archive = Path.Combine(work, "installer.zip");
        await DownloadAsync(url, archive, cancellationToken);
        await using (var input = File.OpenRead(archive))
        {
            Console.WriteLine($"Downloaded {url}, SHA-256 {Convert.ToHexStringLower(await SHA256.HashDataAsync(input, cancellationToken))}");
        }

        // Only the named installer entry is read, so no archive path is ever used as a file name.
        var installer = Path.Combine(work, "installer.jar");
        var name = $"verapdf-greenfield-{version}/verapdf-izpack-installer-{version}.jar";
        await using var zip = await ZipFile.OpenReadAsync(archive, cancellationToken);
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"The veraPDF archive has no {name}.");
        await entry.ExtractToFileAsync(installer, false, cancellationToken);
        return installer;
    }

    /// <summary>Downloads a file within the size limit.</summary>
    /// <param name="url">The URL.</param>
    /// <param name="path">The destination.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidDataException">The file exceeds the limit.</exception>
    private static async Task DownloadAsync(Uri url, string path, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumArchiveBytes)
        {
            throw new InvalidDataException("The veraPDF archive exceeds the download limit.");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(path);
        var buffer = new byte[BufferSize];
        long length = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
        {
            length = checked(length + read);
            if (length > MaximumArchiveBytes)
            {
                throw new InvalidDataException("The veraPDF archive exceeds the download limit.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    /// <summary>Checks the installed validator reports the release, then reports where it is.</summary>
    /// <param name="home">The installation folder.</param>
    /// <param name="version">The release.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidDataException">The validator reports another release.</exception>
    private static async Task ReportAsync(string home, string version, CancellationToken cancellationToken)
    {
        string[] arguments = ["-cp", Path.Combine(home, "bin", "*"), $"-Dapp.home={home}", "org.verapdf.apps.GreenfieldCliWrapper", "--version"];
        var reported = await RunAsync(FindJava(), arguments, cancellationToken);
        if (!reported.Contains($"veraPDF {version}", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The installed validator did not report veraPDF {version}: {reported}");
        }

        var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (!string.IsNullOrWhiteSpace(githubOutput))
        {
            await File.AppendAllLinesAsync(githubOutput, [$"verapdf-home={home}", $"verapdf-version={version}"], cancellationToken);
        }

        Console.WriteLine(reported.Trim());
        Console.WriteLine($"veraPDF home: {home}");
        Console.WriteLine($"Set HYPERPDF_VERAPDF={home} to run the PDF/A validation tests.");
    }

    /// <summary>Gets the command-line validator's jar for a release.</summary>
    /// <param name="home">The installation folder.</param>
    /// <param name="version">The release.</param>
    /// <returns>The path.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string CliJar(string home, string version) => Path.Combine(home, "bin", $"cli-{version}.jar");

    /// <summary>Writes the IzPack unattended answers.</summary>
    /// <param name="path">The answer file.</param>
    /// <param name="installPath">The installation folder.</param>
    private static void WriteAnswers(string path, string installPath)
    {
        var packs = Packs;
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        using var writer = XmlWriter.Create(path, settings);
        writer.WriteStartElement("AutomatedInstallation");
        writer.WriteAttributeString("langpack", "eng");
        WritePanel(writer, "com.izforge.izpack.panels.htmlhello.HTMLHelloPanel", "welcome");
        writer.WriteStartElement("com.izforge.izpack.panels.target.TargetPanel");
        writer.WriteAttributeString("id", "install_dir");
        writer.WriteElementString("installpath", installPath);
        writer.WriteEndElement();
        writer.WriteStartElement("com.izforge.izpack.panels.packs.PacksPanel");
        writer.WriteAttributeString("id", "sdk_pack_select");
        for (var i = 0; i < packs.Length; i++)
        {
            writer.WriteStartElement("pack");
            writer.WriteAttributeString("index", i.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("name", packs[i]);
            writer.WriteAttributeString("selected", i < SelectedPacks ? "true" : "false");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        WritePanel(writer, "com.izforge.izpack.panels.install.InstallPanel", "install");
        WritePanel(writer, "com.izforge.izpack.panels.finish.FinishPanel", "finish");
        writer.WriteEndElement();
    }

    /// <summary>Writes an installer panel element.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="panel">The panel class.</param>
    /// <param name="panelId">The panel id.</param>
    private static void WritePanel(XmlWriter writer, string panel, string panelId)
    {
        writer.WriteStartElement(panel);
        writer.WriteAttributeString("id", panelId);
        writer.WriteEndElement();
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

    /// <summary>Runs a process to completion and returns its output.</summary>
    /// <param name="executable">The program.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">Cancels the run and kills the process.</param>
    /// <returns>The standard output and error text.</returns>
    /// <exception cref="IOException">The process did not start or did not succeed.</exception>
    private static async Task<string> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new IOException($"{executable} did not start.");

        // Read until the owned process exits so the diagnostics remain available after it is killed.
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            var status = await process.WaitForExitStatusAsync(cancellationToken);
            var text = await output + await error;
            return status.Canceled || status.Signal is not null || status.ExitCode != 0
                ? throw new IOException($"{Path.GetFileName(executable)} {string.Join(' ', arguments)} failed: {text}")
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
