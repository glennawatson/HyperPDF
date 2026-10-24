// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks the allocation-free projection of XMP values into report metadata.</summary>
public sealed class AccessibilityMetadataReaderTests
{
    /// <summary>The PDF/UA-1 revision declared in the packet.</summary>
    private const string Revision = "2014";

    /// <summary>No readable packet produces the default metadata value.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_NoPacket_ReturnsDefault() =>
        await Assert.That(AccessibilityMetadataReader.Read(null)).IsEqualTo(default(AccessibilityMetadata));

    /// <summary>Already parsed XMP values do not need another retained metadata object.</summary>
    /// <param name="cancellationToken">Cancels opening the document.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Read_CachedPacket_DoesNotAllocate(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(
            AccessibilityPdfs.Build(new() { Xmp = AccessibilityPdfs.Packet(1, true, Revision) }),
            null,
            cancellationToken);
        _ = PdfDocumentMetadata.GetXmp(document);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var metadata = AccessibilityMetadataReader.Read(PdfDocumentMetadata.GetXmp(document));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(metadata.HasMetadata).IsTrue();
        await Assert.That(metadata.HasTitle).IsTrue();
        await Assert.That(metadata.Part).IsEqualTo(1);
        await Assert.That(metadata.Revision).IsEqualTo(Revision);
        await Assert.That(allocated).IsEqualTo(0);
    }
}
