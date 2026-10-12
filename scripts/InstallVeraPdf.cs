#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

// Installs the veraPDF command-line validator that the PDF/A output tests run as a separate process. veraPDF is
// dual-licensed GPLv3+ and MPLv2+; it is downloaded and run, never linked or redistributed. It needs Java 11 or later.
const string veraPdfVersion = "1.30.3";

const string archiveName = $"verapdf-greenfield-{veraPdfVersion}-installer.zip";

const string archiveUrl = $"https://software.verapdf.org/rel/1.30/{archiveName}";

// veraPDF publishes PGP signatures but no digests; this pins the archive verified on first download.
const string archiveSha256 = "b512bf24945503a33fc10738ecffc1f2dccaaf8593a448e94ee9ac86a9559a59";

const string installerEntry = $"verapdf-greenfield-{veraPdfVersion}/verapdf-izpack-installer-{veraPdfVersion}.jar";

const long maximumArchiveBytes = 128L * 1024 * 1024;

const int setupMinutes = 10;

if (args is not [var destinationArgument])
{
    throw new ArgumentException("Provide the veraPDF installation directory.");
}

var destination = Path.GetFullPath(destinationArgument);

var cliJar = Path.Combine(destination, "bin", $"cli-{veraPdfVersion}.jar");

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(setupMinutes));

if (File.Exists(cliJar))
{
    // A finished install of this version is reused, so local runs do not download it again.
    await ReportAsync(destination, await VerifyAsync(destination, timeout.Token), timeout.Token);
    return;
}

if (Directory.Exists(destination) && Directory.GetFileSystemEntries(destination).Length > 0)
{
    throw new IOException($"The veraPDF installation directory must be empty or hold veraPDF {veraPdfVersion}: {destination}");
}

var work = Path.Combine(Path.GetTempPath(), $"verapdf-setup-{Guid.NewGuid():N}");

_ = Directory.CreateDirectory(work);

try
{
    var archive = Path.Combine(work, archiveName);
    await DownloadAsync(new(archiveUrl), archive, timeout.Token);
    await VerifyHashAsync(archive, timeout.Token);
    var installer = Path.Combine(work, "installer.jar");
    await ExtractInstallerAsync(archive, installer, timeout.Token);
    var answers = Path.Combine(work, "auto-install.xml");
    WriteAnswers(answers, destination);
    await RunAsync(FindJava(), ["-jar", installer, answers], timeout.Token);
    if (!File.Exists(cliJar))
    {
        throw new FileNotFoundException("The veraPDF installer did not write the command-line validator.", cliJar);
    }

    await ReportAsync(destination, await VerifyAsync(destination, timeout.Token), timeout.Token);
}
finally
{
    Directory.Delete(work, true);
}

static async Task DownloadAsync(Uri url, string path, CancellationToken cancellationToken)
{
    const int bufferSize = 65_536;
    using var client = new HttpClient();
    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    _ = response.EnsureSuccessStatusCode();
    if (response.Content.Headers.ContentLength is > maximumArchiveBytes)
    {
        throw new InvalidDataException("The veraPDF archive exceeds the download limit.");
    }

    await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
    await using var output = File.Create(path);
    var buffer = new byte[bufferSize];
    long length = 0;
    int read;
    while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
    {
        length = checked(length + read);
        if (length > maximumArchiveBytes)
        {
            throw new InvalidDataException("The veraPDF archive exceeds the download limit.");
        }

        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
    }
}

static async Task VerifyHashAsync(string path, CancellationToken cancellationToken)
{
    await using var input = File.OpenRead(path);
    var actual = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
    if (!actual.Equals(archiveSha256, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidDataException($"The veraPDF archive does not match its pinned SHA-256; it was {actual}.");
    }
}

static async Task ExtractInstallerAsync(string archive, string installer, CancellationToken cancellationToken)
{
    // Only the named installer entry is read, so no archive path is ever used as a file name.
    await using var zip = await ZipFile.OpenReadAsync(archive, cancellationToken);
    var entry = zip.GetEntry(installerEntry) ?? throw new InvalidDataException($"The veraPDF archive has no {installerEntry}.");
    await entry.ExtractToFileAsync(installer, false, cancellationToken);
}

static void WriteAnswers(string path, string installPath)
{
    // The IzPack unattended answers select the first two packs: the GUI pack, which carries the CLI jar, and the scripts.
    const int selectedPacks = 2;
    string[] packs = ["veraPDF GUI", "veraPDF Mac and *nix Scripts", "veraPDF Validation model", "veraPDF Documentation", "veraPDF Sample Plugins"];
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
        writer.WriteAttributeString("selected", i < selectedPacks ? "true" : "false");
        writer.WriteEndElement();
    }

    writer.WriteEndElement();
    WritePanel(writer, "com.izforge.izpack.panels.install.InstallPanel", "install");
    WritePanel(writer, "com.izforge.izpack.panels.finish.FinishPanel", "finish");
    writer.WriteEndElement();
}

static void WritePanel(XmlWriter writer, string panel, string panelId)
{
    writer.WriteStartElement(panel);
    writer.WriteAttributeString("id", panelId);
    writer.WriteEndElement();
}

static string FindJava()
{
    var executable = OperatingSystem.IsWindows() ? "java.exe" : "java";
    var home = Environment.GetEnvironmentVariable("JAVA_HOME");
    var candidate = string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, "bin", executable);
    return candidate is not null && File.Exists(candidate) ? candidate : executable;
}

static async Task<string> VerifyAsync(string home, CancellationToken cancellationToken)
{
    string[] arguments = ["-cp", Path.Combine(home, "bin", "*"), $"-Dapp.home={home}", "org.verapdf.apps.GreenfieldCliWrapper", "--version"];
    var version = await RunAsync(FindJava(), arguments, cancellationToken);
    return version.Contains($"veraPDF {veraPdfVersion}", StringComparison.Ordinal)
        ? version.Trim()
        : throw new InvalidDataException($"The installed validator did not report veraPDF {veraPdfVersion}: {version}");
}

static async Task ReportAsync(string home, string version, CancellationToken cancellationToken)
{
    var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
    if (!string.IsNullOrWhiteSpace(githubOutput))
    {
        await File.AppendAllLinesAsync(githubOutput, [$"verapdf-home={home}", $"verapdf-version={veraPdfVersion}"], cancellationToken);
    }

    Console.WriteLine(version);
    Console.WriteLine($"veraPDF home: {home}");
    Console.WriteLine($"Set PDFVIEWERLITE_VERAPDF={home} to run the PDF/A validation tests.");
}

static async Task<string> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
{
    const int processCleanupSeconds = 30;
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

        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(processCleanupSeconds));
        _ = await process.WaitForExitStatusAsync(cleanup.Token);
        await Console.Error.WriteLineAsync((await output + await error).AsMemory(), cleanup.Token);
        throw;
    }
}
