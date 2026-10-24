// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.IO;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that external rewrites cannot invalidate a viewer's mapped memory.</summary>
public sealed class HyperPdfExternalRewriteTests
{
    /// <summary>An offset beyond the stream source's cache budget.</summary>
    private const int UncachedOffset = 8 * 1024 * 1024;

    /// <summary>A read larger than the bounded stream cache.</summary>
    private const int ReadLength = 5 * 1024 * 1024;

    /// <summary>The page count after replacement.</summary>
    private const int ReplacementPages = 2;

    /// <summary>A truncated file returns an ordinary short read, not a process-ending memory fault.</summary>
    /// <param name="asynchronous">Whether opening uses the asynchronous adapter.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InPlaceTruncationDoesNotFault(bool asynchronous)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-truncate-{Guid.NewGuid():N}.pdf");
        var bytes = new byte[UncachedOffset + 1];
        bytes.AsSpan().Fill((byte)' ');
        TestPdf.Create(1).CopyTo(bytes, 0);
        await File.WriteAllBytesAsync(path, bytes, CancellationToken.None);
        try
        {
            var engine = new HyperPdfEngine();
            using var document = (HyperPdfDocument)(asynchronous
                ? await engine.OpenAsync(path, null, CancellationToken.None)
                : engine.Open(path, null));
            var source = document.Document.Objects.Source;

            // Assert before truncation so a regression cannot SIGBUS the test process.
            await Assert.That(source).IsTypeOf<StreamPdfByteSource>();
            await using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
                writer.SetLength(0);
            }

            await Assert.That(source.Read(1, new byte[ReadLength])).IsEqualTo(0);
            await File.WriteAllBytesAsync(path, TestPdf.Create(ReplacementPages), CancellationToken.None);
            using var replacement = await engine.OpenAsync(path, null, CancellationToken.None);
            await replacement.PreparePageAsync(ReplacementPages - 1, CancellationToken.None);
            await Assert.That(replacement.PageCount).IsEqualTo(ReplacementPages);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
