#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:include ScriptProcess.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HyperPdf.Scripts;

namespace HyperPdf.Scripts;

/// <summary>
/// Fetches the LGPL build of the FFmpeg libraries that ship with HyperPDF for a runtime and copies the five libraries the player
/// loads into a folder, with the licence text. The build, its address and its SHA-256 come from <c>src/ffmpeg.json</c> in the
/// repository root. The archive is checked against that digest before anything is read from it.
/// </summary>
/// <remarks>
/// Usage: <c>dotnet run --file src/tools/FetchFfmpeg.cs -- &lt;runtime identifier&gt; &lt;destination folder&gt;</c>. A runtime with no build, such
/// as macOS, exits with status 3 and writes nothing, so a caller can carry on without the libraries.
/// </remarks>
internal static class FetchFfmpeg
{
    /// <summary>The file in the repository root that names the build.</summary>
    private const string ManifestFile = "src/ffmpeg.json";

    /// <summary>The exit status for a runtime that has no build.</summary>
    private const int NoBuild = 3;

    /// <summary>The exit status for a bad command line.</summary>
    private const int Usage = 2;

    /// <summary>The largest archive accepted.</summary>
    private const long MaximumArchiveBytes = 256L * 1024 * 1024;

    /// <summary>The download buffer size.</summary>
    private const int BufferSize = 81_920;

    /// <summary>The longest the whole fetch may take.</summary>
    private const int FetchMinutes = 20;

    /// <summary>How many characters of the digest name the marker file.</summary>
    private const int MarkerLength = 12;

    /// <summary>The name the licence is saved under beside the libraries.</summary>
    private const string LicenceName = "LICENSE-FFmpeg.txt";

    /// <summary>The client for the one download.</summary>
    private static readonly HttpClient Client = new();

    /// <summary>Fetches the libraries for a runtime.</summary>
    /// <param name="args">The runtime identifier and the destination folder.</param>
    /// <returns>The exit status.</returns>
    internal static async Task<int> Main(string[] args)
    {
        if (args is not [var runtime, var destinationArgument])
        {
            await Console.Error.WriteLineAsync("Usage: FetchFfmpeg <runtime identifier> <destination folder>");
            return Usage;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(FetchMinutes));
        var manifest = await File.ReadAllBytesAsync(Path.Combine(ScriptProcess.Root(), ManifestFile), timeout.Token);
        using var document = JsonDocument.Parse(manifest);
        var root = document.RootElement;
        if (!root.GetProperty("assets").TryGetProperty(runtime, out var asset))
        {
            Console.WriteLine($"No FFmpeg build is listed for {runtime}; HyperPDF will use FFmpeg programs if they are installed.");
            return NoBuild;
        }

        var destination = Path.GetFullPath(destinationArgument);
        var digest = asset.GetProperty("sha256").GetString()!;
        var marker = Path.Combine(destination, $".ffmpeg-{digest[..MarkerLength]}");
        var files = FileNames(root.GetProperty("files").GetProperty(runtime.StartsWith("win", StringComparison.Ordinal) ? "win" : "linux"));
        var version = root.GetProperty("version").GetString();
        if (File.Exists(marker) && AllExist(destination, files))
        {
            Console.WriteLine($"FFmpeg {version} for {runtime} is already in {destination}.");
            return 0;
        }

        await InstallAsync(asset, files, destination, timeout.Token);
        await File.WriteAllTextAsync(marker, digest, timeout.Token);
        Console.WriteLine($"FFmpeg {version} for {runtime} is in {destination}.");
        return 0;
    }

    /// <summary>Reads the library file names of one platform.</summary>
    /// <param name="names">The array in the manifest.</param>
    /// <returns>The names.</returns>
    private static List<string> FileNames(JsonElement names)
    {
        var files = new List<string>();
        foreach (var name in names.EnumerateArray())
        {
            files.Add(name.GetString()!);
        }

        return files;
    }

    /// <summary>Determines whether every library is already in a folder.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="files">The library file names.</param>
    /// <returns><see langword="true"/> when all are there.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AllExist(string folder, List<string> files) => files.TrueForAll(name => File.Exists(Path.Combine(folder, name)));

    /// <summary>Downloads, checks and unpacks a build.</summary>
    /// <param name="asset">The manifest entry.</param>
    /// <param name="files">The library file names to keep.</param>
    /// <param name="destination">The folder to fill.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>A task.</returns>
    private static async Task InstallAsync(JsonElement asset, List<string> files, string destination, CancellationToken cancellationToken)
    {
        var work = Path.Combine(Path.GetTempPath(), $"ffmpeg-setup-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(work);
        try
        {
            var archive = Path.Combine(work, "ffmpeg-archive");
            var address = new Uri(asset.GetProperty("url").GetString()!);
            await DownloadAsync(address, archive, cancellationToken);
            await CheckDigestAsync(archive, asset.GetProperty("sha256").GetString()!, cancellationToken);
            _ = Directory.CreateDirectory(destination);
            if (asset.GetProperty("kind").GetString() == "zip")
            {
                await UnpackZipAsync(archive, files, destination, cancellationToken);
            }
            else
            {
                await UnpackTarAsync(archive, files, destination, work, cancellationToken);
            }
        }
        finally
        {
            Directory.Delete(work, true);
        }
    }

    /// <summary>Downloads a file within the size limit.</summary>
    /// <param name="url">The address.</param>
    /// <param name="path">Where to save it.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidDataException">The file is larger than the limit.</exception>
    private static async Task DownloadAsync(Uri url, string path, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
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
                throw new InvalidDataException("The FFmpeg archive is larger than the download limit.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    /// <summary>Checks a file against its SHA-256.</summary>
    /// <param name="path">The file.</param>
    /// <param name="expected">The digest the manifest names, in hexadecimal.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidDataException">The digests differ.</exception>
    private static async Task CheckDigestAsync(string path, string expected, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(path);
        var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(input, cancellationToken));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The FFmpeg archive has SHA-256 {actual}, not the {expected} that {ManifestFile} names.");
        }
    }

    /// <summary>Finds the entry of a zip archive that ends with a path.</summary>
    /// <param name="zip">The archive.</param>
    /// <param name="ending">The end of the entry's full name.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    private static ZipArchiveEntry? FindEntry(ZipArchive zip, string ending)
    {
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith(ending, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Copies the libraries and the licence out of a zip archive; only the entries named are read.</summary>
    /// <param name="archive">The archive.</param>
    /// <param name="files">The library file names to keep.</param>
    /// <param name="destination">The folder to fill.</param>
    /// <param name="cancellationToken">Cancels the unpacking.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidDataException">A library is missing.</exception>
    private static async Task UnpackZipAsync(string archive, List<string> files, string destination, CancellationToken cancellationToken)
    {
        await using var zip = await ZipFile.OpenReadAsync(archive, cancellationToken);
        foreach (var name in files)
        {
            var entry = FindEntry(zip, $"/bin/{name}") ?? throw new InvalidDataException($"The FFmpeg archive has no {name}.");
            await entry.ExtractToFileAsync(Path.Combine(destination, name), true, cancellationToken);
        }

        if (FindEntry(zip, "/LICENSE.txt") is { } licence)
        {
            await licence.ExtractToFileAsync(Path.Combine(destination, LicenceName), true, cancellationToken);
        }
    }

    /// <summary>Copies the libraries and the licence out of a tar.xz archive with the system <c>tar</c>.</summary>
    /// <param name="archive">The archive.</param>
    /// <param name="files">The library file names to keep.</param>
    /// <param name="destination">The folder to fill.</param>
    /// <param name="work">A scratch folder.</param>
    /// <param name="cancellationToken">Cancels the unpacking.</param>
    /// <returns>A task.</returns>
    /// <exception cref="IOException">The archive cannot be unpacked or a library is missing.</exception>
    private static async Task UnpackTarAsync(string archive, List<string> files, string destination, string work, CancellationToken cancellationToken)
    {
        var extracted = Path.Combine(work, "extracted");
        _ = Directory.CreateDirectory(extracted);
        List<string> arguments = ["-xJf", archive, "-C", extracted, "--wildcards", "*/LICENSE.txt"];
        foreach (var name in files)
        {
            arguments.Add($"*/lib/{name}*");
        }

        if (await ScriptProcess.RunAsync("tar", arguments, work, cancellationToken) != 0)
        {
            throw new IOException("tar could not unpack the FFmpeg archive.");
        }

        foreach (var folder in Directory.EnumerateDirectories(extracted))
        {
            foreach (var name in files)
            {
                // The name in the archive is a link to the file with the full version; copying follows it.
                File.Copy(Path.Combine(folder, "lib", name), Path.Combine(destination, name), true);
            }

            File.Copy(Path.Combine(folder, "LICENSE.txt"), Path.Combine(destination, LicenceName), true);
        }
    }
}
