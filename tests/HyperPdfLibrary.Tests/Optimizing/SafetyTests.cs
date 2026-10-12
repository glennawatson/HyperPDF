// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Signatures;
using HyperPdfLibrary.Tests.Writing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Signatures, encryption, PDF/A claims, text layers, progress, cancellation and the asynchronous form.</summary>
[NotInParallel]
public sealed class SafetyTests
{
    /// <summary>The scan's width.</summary>
    private const int ScanWidth = 200;

    /// <summary>The scan's height.</summary>
    private const int ScanHeight = 260;

    /// <summary>The recognised word.</summary>
    private const string Word = "Invoice";

    /// <summary>The word's left edge in viewer space.</summary>
    private const float WordLeft = 100;

    /// <summary>The word's top edge in viewer space.</summary>
    private const float WordTop = 100;

    /// <summary>The word's right edge in viewer space.</summary>
    private const float WordRight = 220;

    /// <summary>The word's bottom edge in viewer space.</summary>
    private const float WordBottom = 130;

    /// <summary>The recogniser's confidence.</summary>
    private const float Confidence = 0.9F;

    /// <summary>The pages of the multi-page sample.</summary>
    private const int PageCount = 3;

    /// <summary>A signed document is never rewritten: with nothing to add, it is copied byte for byte.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopiesSignedDocuments()
    {
        var source = SignatureSamples.Signed(SignatureFixtures.Signer, 0, string.Empty);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller);
        await Assert.That(result.Report.Mode).IsEqualTo(PdfOptimizeMode.Copied);
        await Assert.That(result.Report.WasSigned).IsTrue();
        await Assert.That(result.Bytes).IsEquivalentTo(source);
    }

    /// <summary>A text layer on a signed scan is appended as an incremental update that leaves the signed bytes untouched.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppendsTextLayersToSignedScans()
    {
        var source = SignedScan();
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { OcrWords = static _ => Words() });
        await Assert.That(result.Report.Mode).IsEqualTo(PdfOptimizeMode.Incremental);
        await Assert.That(result.Bytes.AsSpan(0, source.Length).SequenceEqual(source)).IsTrue();
        await Assert.That(OptimizerTestKit.Text(result.Bytes)[0]).Contains(Word);
        await Assert.That(SignatureFixtures.ValidateSingle(result.Bytes).DigestValid).IsTrue();
    }

    /// <summary>An image-only page gets an invisible text layer from the recognition hook, and still looks the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsTextLayersToScans()
    {
        var source = TestPdf.CreateScan(OptimizerSamples.Strokes(ScanWidth, ScanHeight), ScanWidth, ScanHeight);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality with { OcrWords = static _ => Words() });
        await Assert.That(OptimizerTestKit.Text(result.Bytes)[0]).Contains(Word);
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>An encrypted document stays encrypted with the same handler, and reads the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsEncryption()
    {
        var source = WritingTestDocuments.Encrypt(TestPdf.Create(PageCount));
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Balanced);
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        await Assert.That(document.IsEncrypted).IsTrue();
        await Assert.That(result.Report.IsEncrypted).IsTrue();
        await Assert.That(OptimizerTestKit.Text(result.Bytes)).IsEquivalentTo(OptimizerTestKit.Text(source));
    }

    /// <summary>Encryption is removed only when asked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesEncryptionWhenAsked()
    {
        var source = WritingTestDocuments.Encrypt(TestPdf.Create(PageCount));
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Balanced with { RemoveEncryption = true });
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        await Assert.That(document.IsEncrypted).IsFalse();
        await Assert.That(OptimizerTestKit.Text(result.Bytes)).IsEquivalentTo(OptimizerTestKit.Text(source));
    }

    /// <summary>
    /// A PDF/A-1 claim keeps a classic cross-reference table and the 1.4 header, and refuses the unembedded text layer
    /// font. The fixture only claims PDF/A-1 and has no output intent, so the copy's claim is removed and reported.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RespectsPdfAOne()
    {
        var source = PdfAOne();
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Balanced with { OcrWords = static _ => Words() });
        var text = Encoding.Latin1.GetString(result.Bytes);
        await Assert.That(result.Report.PdfAPart).IsEqualTo(1);
        await Assert.That(result.Report.PdfAClaim!.Removed).IsTrue();
        await Assert.That(text).DoesNotContain("/ObjStm");
        await Assert.That(text).StartsWith("%PDF-1.4");
        await Assert.That(result.Report.Skipped.Any(static skip => skip.Category == PdfOptimizeCategory.Layout)).IsTrue();
        await Assert.That(result.Report.Skipped.Any(static skip => skip.Category == PdfOptimizeCategory.TextLayer)).IsTrue();
    }

    /// <summary>Progress goes through every phase in order, and the asynchronous form writes the same file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsProgressAndWritesAsynchronously()
    {
        var source = TestPdf.Create(PageCount);
        var phases = new List<PdfOptimizePhase>();
        var progress = new SynchronousProgress(phases);
        byte[] asyncBytes;
        using (var document = PdfDocumentReader.Open(source, null))
        {
            await using var output = new MemoryStream();
            _ = await PdfOptimizer.OptimizeAsync(document, output, PdfOptimizeOptions.Balanced, progress, CancellationToken.None);
            asyncBytes = output.ToArray();
        }

        var sync = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Balanced);
        await Assert.That(phases[0]).IsEqualTo(PdfOptimizePhase.Checking);
        await Assert.That(phases[^1]).IsEqualTo(PdfOptimizePhase.Done);
        await Assert.That(phases).Contains(PdfOptimizePhase.Writing);
        await Assert.That(asyncBytes.Length).IsEqualTo(sync.Bytes.Length);
        await Assert.That(OptimizerTestKit.Text(asyncBytes)).IsEquivalentTo(OptimizerTestKit.Text(source));
    }

    /// <summary>A cancelled token stops the run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StopsWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var document = PdfDocumentReader.Open(TestPdf.Create(PageCount), null);
        await using var output = new MemoryStream();
        await Assert.That(() => PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.Balanced, null, cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>The source document is never changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LeavesTheSourceAlone()
    {
        using var document = PdfDocumentReader.Open(OptimizerSamples.UntaggedText(null), null);
        await using var output = new MemoryStream();
        _ = PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.Smaller with { AddInferredTags = true, Cleanup = PdfCleanupItems.All, Language = "en" });
        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsFalse();
        await Assert.That(document.Catalog.ContainsKey(KnownName.StructTreeRoot)).IsFalse();
    }

    /// <summary>Gets the recognised words.</summary>
    /// <returns>One word.</returns>
    private static PdfOcrWord[] Words() => [new(Word, new(WordLeft, WordTop, WordRight, WordBottom), Confidence)];

    /// <summary>Builds a scan signed in an incremental update.</summary>
    /// <returns>The signed file.</returns>
    private static byte[] SignedScan()
    {
        var scan = TestPdf.CreateScan(OptimizerSamples.Strokes(ScanWidth, ScanHeight), ScanWidth, ScanHeight);
        var update = new Dictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R /AcroForm 7 0 R >>",
            [7] = "<< /Fields [8 0 R] /SigFlags 3 >>",
            [8] = "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /F 132 /P 3 0 R /V 9 0 R >>",
            [9] = PdfSigning.SignatureDictionary("adbe.pkcs7.detached", string.Empty),
        };
        return PdfSigning.SignDetached(IncrementalPdf.Append(scan, update), SignatureFixtures.Signer);
    }

    /// <summary>Builds a PDF 1.4 document whose XMP claims PDF/A-1b.</summary>
    /// <returns>The file.</returns>
    private static byte[] PdfAOne()
    {
        const string Xmp =
            "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
            + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">"
            + "<pdfaid:part>1</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";
        var scan = Encoding.Latin1.GetString(TestPdf.CreateScan(OptimizerSamples.Strokes(ScanWidth, ScanHeight), ScanWidth, ScanHeight)).Replace("%PDF-1.7", "%PDF-1.4", StringComparison.Ordinal);
        return IncrementalPdf.Append(
            Encoding.Latin1.GetBytes(scan),
            new Dictionary<int, string> { [1] = "<< /Type /Catalog /Pages 2 0 R /Metadata 7 0 R >>", [7] = MiniPdf.Stream("/Type /Metadata /Subtype /XML", Xmp), });
    }

    /// <summary>Records progress phases on the reporting thread, unlike <see cref="Progress{T}"/>, which posts them later.</summary>
    /// <param name="phases">Receives the phases.</param>
    private sealed class SynchronousProgress(List<PdfOptimizePhase> phases) : IProgress<PdfOptimizeProgress>
    {
        /// <inheritdoc/>
        public void Report(PdfOptimizeProgress value) => phases.Add(value.Phase);
    }
}
