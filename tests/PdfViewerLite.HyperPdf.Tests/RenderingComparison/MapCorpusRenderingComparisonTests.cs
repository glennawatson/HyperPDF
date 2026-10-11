// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;

namespace PdfViewerLite.HyperPdf.Tests.RenderingComparison;

/// <summary>Compares the checksum-pinned public-domain maps with all three renderers.</summary>
[NotInParallel]
public sealed class MapCorpusRenderingComparisonTests
{
    /// <summary>The environment variable for the separate map cache.</summary>
    private const string DirectoryVariable = "PDFVIEWERLITE_RENDER_CORPUS_DIR";

    /// <summary>Requires downloaded map fixtures when set to 1.</summary>
    private const string RequireVariable = "PVL_REQUIRE_RENDER_CORPUS";

    /// <summary>The buffer size used while verifying a cached map.</summary>
    private const int BufferSize = 65_536;

    /// <summary>Checks the exact USGS bytes before saving page comparison evidence.</summary>
    /// <param name="cancellationToken">Cancels file verification and browser rendering.</param>
    /// <returns>A task for the verified comparison.</returns>
    /// <exception cref="FileNotFoundException">A required map is absent from the cache.</exception>
    [Test]
    public async Task PinnedPublicDomainMapsProduceThreeEngineEvidence(CancellationToken cancellationToken)
    {
        var folder = Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "render-corpus");
        await using var manifest = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "render-corpus.json"));
        using var json = await JsonDocument.ParseAsync(manifest, cancellationToken: cancellationToken);
        var entries = json.RootElement.GetProperty("documents");
        foreach (var entry in entries.EnumerateArray())
        {
            var path = Path.Combine(folder, $"{entry.GetProperty("id").GetString()}.pdf");
            if (File.Exists(path))
            {
                continue;
            }

            if (Environment.GetEnvironmentVariable(RequireVariable) == "1")
            {
                throw new FileNotFoundException($"Fetch the pinned render corpus before running the required comparison: {path}", path);
            }

            Skip.Test($"The pinned render corpus is missing {path}.");
        }

        CorpusRenderingComparisonTests.RequireBrowser();
        await using var browser = await PdfJsBrowserSession.CreateAsync(cancellationToken);
        foreach (var entry in entries.EnumerateArray())
        {
            var path = Path.Combine(folder, $"{entry.GetProperty("id").GetString()}.pdf");
            await VerifyAsync(path, entry, cancellationToken);
            var pages = await CorpusRenderingComparisonTests.CompareFileAsync(path, browser, cancellationToken);
            await Assert.That(pages).IsEqualTo(entry.GetProperty("pageCount").GetInt32());
        }
    }

    /// <summary>Rejects changed files before they are used as comparison evidence.</summary>
    /// <param name="path">The locally cached PDF path.</param>
    /// <param name="entry">The manifest row with its exact size and digest.</param>
    /// <param name="cancellationToken">Cancels the digest read.</param>
    /// <returns>A task for the content check.</returns>
    /// <exception cref="InvalidDataException">The cached map differs from the pinned manifest.</exception>
    private static async Task VerifyAsync(string path, JsonElement entry, CancellationToken cancellationToken)
    {
        var expectedLength = entry.GetProperty("bytes").GetInt64();
        if (new FileInfo(path).Length != expectedLength)
        {
            throw new InvalidDataException($"The render corpus file has changed size: {path}");
        }

        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actual = await SHA256.HashDataAsync(file, cancellationToken);
        var expected = Convert.FromHexString(entry.GetProperty("sha256").GetString()!);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw new InvalidDataException($"The render corpus file does not match its pinned SHA-256: {path}");
        }
    }
}
