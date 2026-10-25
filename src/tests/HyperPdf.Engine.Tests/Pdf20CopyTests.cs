// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the engine's PDF 2.0 copy writer: a 2.0 header, the same pages and properties, an untouched open file, and refusals that write nothing.</summary>
public sealed class Pdf20CopyTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>Gets the bytes a PDF 2.0 file starts with.</summary>
    private static ReadOnlySpan<byte> Header => "%PDF-2.0"u8;

    /// <summary>A copy has a 2.0 header, the same pages and the same document properties, and the open file is untouched.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopyIsPdf20WithTheSamePagesAndProperties()
    {
        using var source = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var before = await File.ReadAllBytesAsync(source.FilePath);
        var writer = DocumentFeature<IPdf20CopyWriter>.Cast(source.Document)!;
        await using var output = new MemoryStream();

        var result = await writer.WritePdf20CopyAsync(output, CancellationToken.None);

        await Assert.That(result.Converted).IsTrue().Because(string.Join("; ", result.Problems.Select(static p => p.Description)));
        var bytes = output.ToArray();
        await Assert.That(bytes.AsSpan().StartsWith(Header)).IsTrue();
        await Assert.That(await File.ReadAllBytesAsync(source.FilePath)).IsEquivalentTo(before);
        using var copy = PdfDocumentReader.Open(bytes, null);
        await Assert.That(copy.PageCount).IsEqualTo(Pages);
        var copyPath = Path.Combine(Path.GetTempPath(), $"hyperpdf-pdf20-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(copyPath, bytes);
        try
        {
            using var reopened = new HyperPdfEngine().Open(copyPath, null);
            var original = source.Document.GetMetadata();
            var copied = reopened.GetMetadata();
            await Assert.That(copied with { FormatVersion = null }).IsEqualTo(original with { FormatVersion = null });
            await Assert.That(copied.FormatVersion).IsEqualTo("2.0");
            await Assert.That(original.Title).IsNotNull();
        }
        finally
        {
            File.Delete(copyPath);
        }
    }

    /// <summary>A document the copy cannot keep is refused with a reason and nothing is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusedCopyWritesNothing()
    {
        var needsRendering = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /NeedsRendering true >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        using var source = new HyperPdfTestDocument(needsRendering);
        var writer = DocumentFeature<IPdf20CopyWriter>.Cast(source.Document)!;
        await using var output = new MemoryStream();

        var result = await writer.WritePdf20CopyAsync(output, CancellationToken.None);

        await Assert.That(result.Converted).IsFalse();
        await Assert.That(result.Problems).IsNotEmpty();
        await Assert.That(result.Problems[0].Description).Contains("XFA");
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>A cancelled token stops the copy before anything is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationStopsTheCopy()
    {
        using var source = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var writer = DocumentFeature<IPdf20CopyWriter>.Cast(source.Document)!;
        await using var output = new MemoryStream();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(async () => await writer.WritePdf20CopyAsync(output, cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(output.Length).IsEqualTo(0);
    }
}
