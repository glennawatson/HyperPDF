// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Checks that one document's decoder lifetime does not affect another document.</summary>
public sealed class PdfStreamDecoderOwnershipTests
{
    /// <summary>The width and height of the test page.</summary>
    private const int PageSize = 100;

    /// <summary>Closing one store leaves a second store's Flate stream readable.</summary>
    /// <param name="testToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosingOneStoreKeepsOtherStoreDecoderUsable(CancellationToken testToken)
    {
        var original = "Independent document decoder state"u8.ToArray();
        var encoded = Compress(original);
        var pdf = new RenderTestPdf(PageSize, PageSize);
        var streamNumber = pdf.AddStream("/Filter /FlateDecode", encoded);
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/Metadata {streamNumber} 0 R");
        var bytes = pdf.ToBytes();
        using var first = PdfDocumentReader.Open(bytes, null);
        using var second = PdfDocumentReader.Open(bytes, null);
        var firstStream = first.Catalog.GetStream(KnownName.Metadata)!;
        var secondStream = second.Catalog.GetStream(KnownName.Metadata)!;

        await Assert.That(first.Objects.FlateDecoders is not null && ReferenceEquals(first.Objects.FlateDecoders, second.Objects.FlateDecoders)).IsFalse();
        await Assert.That(firstStream.DecodeToArray(testToken)).IsEquivalentTo(original);
        await Assert.That(secondStream.DecodeToArray(testToken)).IsEquivalentTo(original);

        first.Objects.Dispose();
        await Assert.That(secondStream.DecodeToArray(testToken)).IsEquivalentTo(original);
    }

    /// <summary>Compresses bytes for a PDF Flate stream.</summary>
    /// <param name="source">The bytes to compress.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] Compress(byte[] source)
    {
        var output = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(source, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }
}
