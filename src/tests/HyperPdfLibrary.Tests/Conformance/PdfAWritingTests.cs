// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Tests.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Tests that the writers keep a PDF/A claim valid: the PDF/A-1 file layout, plain metadata and signed files.</summary>
public sealed class PdfAWritingTests
{
    /// <summary>The DocMDP permission of the certified sample, which allows form filling.</summary>
    private const int FormFilling = 2;

    /// <summary>A PDF/A-1 file rewritten compactly keeps a classic table and no object streams, from the sync and async paths (#177).</summary>
    /// <param name="useAsync">Whether to save asynchronously.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CompactPdfA1KeepsAClassicTable(bool useAsync)
    {
        var saved = await CompactAsync(PdfAFixtures.Create(PdfAFixtureLevel.A1B), PdfCompactOptions.Default, useAsync);
        var text = Encoding.Latin1.GetString(saved);
        using var reopened = PdfDocumentReader.Open(saved, null);
        await Assert.That(text).StartsWith("%PDF-1.4");
        await Assert.That(text).Contains("\ntrailer\n");
        await Assert.That(text).DoesNotContain("/ObjStm");
        await Assert.That(text).DoesNotContain("/XRef");
        await Assert.That(PdfDocumentConformance.GetConformance(reopened).PdfA?.Label).IsEqualTo("1B");
    }

    /// <summary>PDF/A-2 and later allow object streams, so the default layout keeps them.</summary>
    /// <param name="part">The PDF/A part.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(2)]
    [Arguments(4)]
    public async Task CompactLaterPartsKeepObjectStreams(int part)
    {
        using var store = StoreOpening.Open(PdfAFixtures.Create(part == PdfAFixtureLevel.Part4 ? PdfAFixtureLevel.A4 : PdfAFixtureLevel.A2B), null);
        await Assert.That(Encoding.Latin1.GetString(PdfCompactWriter.Save(store, PdfCompactOptions.Default))).Contains("/ObjStm");
    }

    /// <summary>A filtered catalog metadata stream is written plain, as PDF/A-1 requires and XMP readers expect.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactWritesFilteredMetadataPlain()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A1B), null);
        var raw = document.Catalog.GetRaw(KnownName.Metadata);
        var stream = document.Catalog.GetStream(KnownName.Metadata)!;
        var packet = stream.DecodeToArray(CancellationToken.None);
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(packet, ref compressed);
            var dictionary = stream.Dictionary.Clone();
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            StoreEditing.Replace(document.Objects, raw.AsReference(), PdfValue.FromStream(new(dictionary, compressed.ToArray())));
        }
        finally
        {
            compressed.Dispose();
        }

        var saved = PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Classic);
        using var reopened = PdfDocumentReader.Open(saved, null);
        await Assert.That(reopened.Catalog.GetStream(KnownName.Metadata)!.Dictionary.ContainsKey(KnownName.Filter)).IsFalse();
        await Assert.That(Encoding.Latin1.GetString(saved)).Contains("<pdfaid:part>1</pdfaid:part>");
    }

    /// <summary>The builder writes the requested header and keeps metadata plain.</summary>
    /// <param name="part">The PDF/A part, which picks the header.</param>
    /// <param name="header">The expected header.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "%PDF-1.4")]
    [Arguments(2, "%PDF-1.7")]
    [Arguments(4, "%PDF-2.0")]
    public async Task BuilderWritesTheVersionAndPlainMetadata(int part, string header)
    {
        var text = Encoding.Latin1.GetString(PdfAFixtures.Create(new(part, part == PdfAFixtureLevel.Part4 ? null : "B")));
        await Assert.That(text).StartsWith(header);
        await Assert.That(text).Contains("<pdfaid:part>");
    }

    /// <summary>Full rewrites refuse a signed file with a reason, and incremental saving still keeps the signed bytes (#179).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FullRewritesRefuseASignedFile()
    {
        var signed = SignatureSamples.Signed(SignatureFixtures.Signer, FormFilling, string.Empty);
        using var document = PdfDocumentReader.Open(signed, null);
        await using var destination = new MemoryStream();
        var compact = await Assert.ThrowsAsync<PdfException>(() => PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, destination, CancellationToken.None));
        await Assert.That(compact?.Message).Contains("signed");
        await Assert.That(destination.Length).IsEqualTo(0);
        await Assert.ThrowsAsync<PdfException>(() => PdfLinearizedWriter.SaveAsync(document, destination, CancellationToken.None));
        _ = Assert.Throws<PdfException>(() => PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Classic));
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "Signed and edited" });
        var incremental = PdfIncrementalWriter.Save(document.Objects);
        var report = SignatureFixtures.ValidateSingle(incremental);
        await Assert.That(CryptographicOperations.FixedTimeEquals(incremental.AsSpan(0, signed.Length), signed)).IsTrue();
        await Assert.That(report.ByteRange.Status).IsEqualTo(PdfByteRangeStatus.Valid);
        await Assert.That(report.Cms.DigestValid).IsTrue();
    }

    /// <summary>Removing the signatures is an edit that can be undone, and leaves a file the compact writer can rewrite.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovedSignaturesAllowAnUnsignedCopy()
    {
        using var document = PdfDocumentReader.Open(SignatureSamples.Signed(SignatureFixtures.Signer, FormFilling, string.Empty), null);
        var removed = PdfDocumentSignatureEditing.RemoveSignatures(document);
        var copy = PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
        using var reopened = StoreOpening.Open(copy, null);
        await Assert.That(removed).IsEqualTo(FormFilling);
        await Assert.That(SignaturePresence.HasSignature(reopened)).IsFalse();
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(SignaturePresence.HasSignature(document.Objects)).IsTrue();
    }

    /// <summary>Saves compactly through the sync or async path.</summary>
    /// <param name="file">The file.</param>
    /// <param name="options">The layout.</param>
    /// <param name="useAsync">Whether to use the async path.</param>
    /// <returns>The saved file.</returns>
    private static async Task<byte[]> CompactAsync(byte[] file, PdfCompactOptions options, bool useAsync)
    {
        using var store = StoreOpening.Open(file, null);
        if (!useAsync)
        {
            return PdfCompactWriter.Save(store, options);
        }

        await using var stream = new MemoryStream();
        await PdfCompactWriter.SaveAsync(store, options, stream, CancellationToken.None);
        return stream.ToArray();
    }
}
