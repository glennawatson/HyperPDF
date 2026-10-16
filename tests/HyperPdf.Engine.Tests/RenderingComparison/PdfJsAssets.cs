// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security.Cryptography;
using Refit;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Loads the checksum-pinned official browser distribution into an external cache.</summary>
internal static class PdfJsAssets
{
    /// <summary>The renderer release used by the comparison suite.</summary>
    internal const string Version = "6.3.289";

    /// <summary>The exact official generic browser distribution.</summary>
    internal const string DownloadUrl = "https://github.com/mozilla/pdf.js/releases/download/v6.3.289/pdfjs-6.3.289-dist.zip";

    /// <summary>The SHA256 of the official release archive.</summary>
    internal const string ArchiveSha256 = "98C5832FFE7AF4EDD59853476A478C0D4D4D76DD49C1701F4C86F7182725CDF9";

    /// <summary>The maximum attempts after transient HTTP failures.</summary>
    private const int DownloadAttempts = 3;

    /// <summary>The maximum accepted distribution archive length.</summary>
    private const int MaximumArchiveBytes = 64 * 1024 * 1024;

    /// <summary>The retry backoff after a transient HTTP failure.</summary>
    private const int RetryMilliseconds = 250;

    /// <summary>The shared transport for pinned asset downloads.</summary>
    private static readonly HttpClient Client = CreateClient();

    /// <summary>The generated client for the pinned release endpoint.</summary>
    private static readonly IPdfJsDownloadApi DownloadApi = RestService.For<IPdfJsDownloadApi>(Client);

    /// <summary>Returns verified browser files; cached bytes are checked on every session.</summary>
    /// <param name="cancellationToken">Cancels cache reading and downloading.</param>
    /// <returns>Browser paths and their owned file bytes.</returns>
    internal static async Task<Dictionary<string, byte[]>> LoadAsync(CancellationToken cancellationToken)
    {
        var cache = GetCacheDirectory();
        _ = Directory.CreateDirectory(cache);
        var archivePath = Path.Combine(cache, "browser-dist.zip");
        var archive = File.Exists(archivePath) ? await File.ReadAllBytesAsync(archivePath, cancellationToken) : Array.Empty<byte>();
        if (!IsVerified(archive))
        {
            archive = await DownloadAsync(cancellationToken);
            var temporaryPath = $"{archivePath}.{Guid.NewGuid():N}";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, archive, cancellationToken);
                File.Move(temporaryPath, archivePath, true);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }

        return ReadArchive(archive);
    }

    /// <summary>Checks the exact pinned archive checksum.</summary>
    /// <param name="archive">The archive.</param>
    /// <returns>The operation result.</returns>
    private static bool IsVerified(byte[] archive) =>
            archive.Length is > 0 and <= MaximumArchiveBytes && Convert.ToHexString(SHA256.HashData(archive)) == ArchiveSha256;

    /// <summary>Downloads and verifies the pinned browser distribution.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The operation result.</returns>
    /// <exception cref="InvalidDataException">The archive checksum differs from the pinned release.</exception>
    /// <exception cref="IOException">The bounded download attempts fail.</exception>
    private static async Task<byte[]> DownloadAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < DownloadAttempts; attempt++)
        {
            try
            {
                using var response = await DownloadApi.GetArchiveAsync(cancellationToken);
                _ = response.EnsureSuccessStatusCode();
                var archive = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (!IsVerified(archive))
                {
                    throw new InvalidDataException("The pdf.js distribution does not match its pinned SHA256.");
                }

                return archive;
            }
            catch (Exception exception) when (attempt < DownloadAttempts - 1 && !cancellationToken.IsCancellationRequested && exception is HttpRequestException or IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(RetryMilliseconds), cancellationToken);
            }
        }

        throw new IOException("The pdf.js distribution could not be downloaded.");
    }

    /// <summary>Reads verified browser assets without extracting paths.</summary>
    /// <param name="archive">The archive.</param>
    /// <returns>The operation result.</returns>
    /// <exception cref="InvalidDataException">An archive entry has an unsafe path or length.</exception>
    private static Dictionary<string, byte[]> ReadArchive(byte[] archive)
    {
        var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            if (entry.FullName.Contains("..", StringComparison.Ordinal) || entry.FullName.Contains('\\') || entry.Length > MaximumArchiveBytes)
            {
                throw new InvalidDataException("Unsafe pdf.js archive entry.");
            }

            using var source = entry.Open();
            using var bytes = new MemoryStream();
            source.CopyTo(bytes);
            assets.Add($"/{entry.FullName}", bytes.ToArray());
        }

        return assets;
    }

    /// <summary>Creates the shared remote asset transport.</summary>
    /// <returns>The configured transport.</returns>
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { BaseAddress = new("https://github.com") };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HyperPDF-render-comparison");
        return client;
    }

    /// <summary>Returns an absolute external cache directory.</summary>
    /// <returns>The browser asset cache directory.</returns>
    /// <exception cref="IOException">The user cache path cannot be determined.</exception>
    private static string GetCacheDirectory()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(folder) || !Path.IsPathFullyQualified(folder))
        {
            throw new IOException("An absolute user cache directory is required for pdf.js assets.");
        }

        return Path.Combine(folder, "HyperPDF", "pdfjs", Version);
    }
}
