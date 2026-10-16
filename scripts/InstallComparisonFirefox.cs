#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

// Installs the browser used by the Ubuntu rendering comparison job into its temporary directory.
const string firefoxVersion = "157.0.1";

const string archiveName = $"firefox-{firefoxVersion}.tar.xz";

const string archiveSha512 = "ebec5e5c91a7c954a0f276928748bb4249e2b44bccdcfe6c7d7b4ed9c64ca6891a56cc8a016546bfbf1ca537dfe6084dba8f2746037c62fae0acc13a542421f3";

const long maximumArchiveBytes = 256L * 1024 * 1024;

const int setupMinutes = 5;

const int downloadAttempts = 3;

const int retryMilliseconds = 500;

if (args is not [var destinationArgument])
{
    throw new ArgumentException("Provide a fresh browser installation directory.");
}

if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
{
    throw new PlatformNotSupportedException("This installer serves the Ubuntu x64 rendering comparison job.");
}

var destination = Path.GetFullPath(destinationArgument);

if (Directory.Exists(destination))
{
    throw new IOException("The browser installation directory must be fresh.");
}

_ = Directory.CreateDirectory(destination);

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(setupMinutes));

using var client = new HttpClient();

const string url = $"https://archive.mozilla.org/pub/firefox/releases/{firefoxVersion}/linux-x86_64/en-US/{archiveName}";

var archive = Path.Combine(destination, archiveName);

await DownloadArchiveAsync(client, new(url), archive, timeout.Token);

await using (var input = File.OpenRead(archive))
{
    var actualHash = Convert.ToHexString(await SHA512.HashDataAsync(input, timeout.Token));
    if (!actualHash.Equals(archiveSha512, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidDataException("Firefox archive does not match Mozilla's pinned SHA512.");
    }
}

// tar preserves the executable modes in the checksum-verified Mozilla archive.
await RunAsync("tar", ["-xJf", archive, "-C", destination], Path.Combine(destination, "extraction.log"), timeout.Token);

var executable = Path.Combine(destination, "firefox", "firefox");

if (!File.Exists(executable))
{
    throw new FileNotFoundException("The verified archive did not contain the Firefox executable.", executable);
}

await RunAsync(executable, ["--version"], Path.Combine(destination, "version.log"), timeout.Token);

var versionOutput = await File.ReadAllTextAsync(Path.Combine(destination, "version.log"), timeout.Token);

if (!versionOutput.Contains($"Mozilla Firefox {firefoxVersion}", StringComparison.Ordinal))
{
    throw new InvalidDataException("The installed browser reported an unexpected version.");
}

await File.WriteAllLinesAsync(Path.Combine(destination, "source.txt"), [url, $"SHA512 {archiveSha512}"], timeout.Token);

var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");

if (!string.IsNullOrWhiteSpace(githubOutput))
{
    await File.AppendAllLinesAsync(githubOutput, [$"firefox-path={executable}", $"firefox-version={firefoxVersion}"], timeout.Token);
}

Console.WriteLine(versionOutput.Trim());

Console.WriteLine($"Verified Firefox executable: {executable}");

static async Task DownloadArchiveAsync(HttpClient client, Uri uri, string archive, CancellationToken cancellationToken)
{
    for (var attempt = 0; attempt < downloadAttempts; attempt++)
    {
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > maximumArchiveBytes)
            {
                throw new InvalidDataException("Firefox archive exceeds the download limit.");
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(archive);
            const int bufferSize = 65_536;
            var buffer = new byte[bufferSize];
            long length = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
            {
                length = checked(length + read);
                if (length > maximumArchiveBytes)
                {
                    throw new InvalidDataException("Firefox archive exceeds the download limit.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            return;
        }
        catch (Exception exception) when (attempt < downloadAttempts - 1 && !cancellationToken.IsCancellationRequested && exception is HttpRequestException or IOException)
        {
            File.Delete(archive);
            await Task.Delay(TimeSpan.FromMilliseconds(retryMilliseconds * (attempt + 1)), cancellationToken);
        }
    }
}

static async Task RunAsync(string executable, string[] arguments, string logPath, CancellationToken cancellationToken)
{
    const int processCleanupSeconds = 30;
    var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start) ?? throw new IOException("The browser setup command did not start.");

    // Read until the owned process exits so cancellation diagnostics remain available after it is killed.
    var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
    var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
    try
    {
        _ = await process.WaitForExitStatusAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(processCleanupSeconds));
        _ = await process.WaitForExitStatusAsync(cleanup.Token);
        await File.WriteAllTextAsync(logPath, await output + await error, cleanup.Token);
        throw;
    }

    await File.WriteAllTextAsync(logPath, await output + await error, cancellationToken);
    var status = await process.WaitForExitStatusAsync(cancellationToken);
    if (status.Canceled || status.Signal is not null || status.ExitCode != 0)
    {
        throw new IOException($"Browser setup failed. Read {logPath}");
    }
}
