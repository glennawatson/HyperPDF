// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks that font data is demanded by content, including nested forms and graphics states.</summary>
public sealed class FontDataDemandTests
{
    /// <summary>The page size.</summary>
    private const int Edge = 100;

    /// <summary>The resource group used for refresh tests.</summary>
    private const string CacheFolder = "CMaps";

    /// <summary>Content that selects no font.</summary>
    private const string GraphicsContent = "0 0 10 10 re f";

    /// <summary>A font dictionary used by the fixtures.</summary>
    private const string Font = "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /90ms-RKSJ-H /DescendantFonts [] >>";

    /// <summary>An unused CJK resource does not request any font data.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IgnoresUnusedFonts()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Resources = $"/Font << /F1 {Font} >>", Content = GraphicsContent };
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        await Assert.That(FontDataDemand.Collect(PdfDocumentPages.GetPage(document, 0), CancellationToken.None)).IsEmpty();
        await PdfDocumentPages.PrefetchPageAsync(document, 0, CancellationToken.None);
    }

    /// <summary>Newly published resources invalidate text extracted before their availability.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefreshesTextAfterResourcesArePublished()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = GraphicsContent };
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        var before = PdfDocumentText.GetTextPage(document, 0);
        using var cache = new FontDataTestCache();
        await FontDataResources.EnsureAsync(
            document.DrawingServices.FontResources.Data,
            cache.DirectoryPath,
            CacheFolder,
            "refresh.bin",
            static _ => ValueTask.FromResult<byte[]>([1]),
            CancellationToken.None);
        await PdfDocumentPages.PrefetchPageAsync(document, 0, CancellationToken.None);
        await Assert.That(PdfDocumentText.GetTextPage(document, 0)).IsNotSameReferenceAs(before);
    }

    /// <summary>A document drops cached text after another process publishes a requested asset.</summary>
    /// <returns>A task.</returns>
    [Test]
    [NotInParallel]
    public async Task RefreshesTextAfterExternalPublication()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = GraphicsContent };
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        await PdfDocumentPages.PrefetchPageAsync(document, 0, CancellationToken.None);
        var before = PdfDocumentText.GetTextPage(document, 0);
        using var cache = new FontDataTestCache();
        const string name = "external-refresh.bin";
        using var missing = FontDataResources.Open(cache.DirectoryPath, CacheFolder, name);
        await Assert.That(missing).IsNull();
        var path = Path.Combine(cache.DirectoryPath, CacheFolder, name);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, [1]);
        await FontDataResources.EnsureAsync(
            document.DrawingServices.FontResources.Data,
            cache.DirectoryPath,
            CacheFolder,
            name,
            static _ => throw new InvalidOperationException("The resource is already available."),
            CancellationToken.None);

        await PdfDocumentPages.PrefetchPageAsync(document, 0, CancellationToken.None);
        await Assert.That(PdfDocumentText.GetTextPage(document, 0)).IsNotSameReferenceAs(before);
    }

    /// <summary>A caller retries when another caller cancelled shared preparation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RetriesAnotherCallersCancellation()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = GraphicsContent };
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        using var owner = new CancellationTokenSource();
        await owner.CancelAsync();
        await FontDataPrefetcher.WaitForPreparationAsync(document, PdfDocumentPages.GetPage(document, 0), Task.FromCanceled(owner.Token), owner.Token, CancellationToken.None);
        await PdfDocumentPages.PrefetchPageAsync(document, 0, CancellationToken.None);
    }

    /// <summary>A Tf operator selects only its named font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsSelectedFont()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Resources = $"/Font << /F1 {Font} /Unused {Font} >>", Content = "BT /F1 12 Tf (text) Tj ET" };
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        var page = PdfDocumentPages.GetPage(document, 0);
        var fonts = FontDataDemand.Collect(page, CancellationToken.None);

        await Assert.That(fonts.Count).IsEqualTo(1);
        await Assert.That(fonts.Single()).IsSameReferenceAs(page.Resources!.GetDictionary(KnownName.Font)!.GetDictionary(document.Objects.Names.Intern("F1")));
    }

    /// <summary>Referenced forms inherit resources and unreferenced forms are ignored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsFontInReferencedForm()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "/Used Do" };
        var used = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 100 100]", "BT /F1 12 Tf (text) Tj ET");
        var unused = pdf.AddStream($"/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Resources << /Font << /F2 {Font} >> >>", "BT /F2 12 Tf (unused) Tj ET");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Font << /F1 {Font} >> /XObject << /Used {used} 0 R /Unused {unused} 0 R >>");
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        await Assert.That(FontDataDemand.Collect(PdfDocumentPages.GetPage(document, 0), CancellationToken.None).Count).IsEqualTo(1);
    }

    /// <summary>ExtGState font arrays demand their font even without Tf.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsGraphicsStateFont()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Resources = $"/ExtGState << /State << /Font [{Font} 12] >> >>", Content = "/State gs BT (text) Tj ET" };
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        await Assert.That(FontDataDemand.Collect(PdfDocumentPages.GetPage(document, 0), CancellationToken.None).Count).IsEqualTo(1);
    }
}
