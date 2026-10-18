// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests page and resource traversal through raw circular and over-deep references.</summary>
public sealed class PageTreeReaderTests
{
    /// <summary>The number of intermediate nodes in the hostile tree.</summary>
    private const int HostileDepth = 10_000;

    /// <summary>The first page number after the catalog and tree root.</summary>
    private const int PageNumber = 3;

    /// <summary>The resource dictionary object number.</summary>
    private const int ResourceNumber = 4;

    /// <summary>The other ancestor in the circular parent chain.</summary>
    private const int OtherParent = 5;

    /// <summary>The catalog naming the root page tree.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>A leaf page that inherits from the root.</summary>
    private const string Page = "<< /Type /Page /Parent 2 0 R >>";

    /// <summary>Mutually linked Kids stop and retain a valid root sibling.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Read_CircularKids_KeepsValidSibling()
    {
        var diagnostics = new List<PdfDiagnostic>();
        using var document = PdfDocumentReader.OpenWith(
            MiniPdf.Build(
                Catalog,
                "<< /Type /Pages /Kids [4 0 R 3 0 R] /Count 1 >>",
                Page,
                "<< /Type /Pages /Kids [2 0 R] /Count 1 >>"),
            new() { Diagnostics = diagnostics.Add });

        await Assert.That(document.PageCount).IsEqualTo(1);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Id.Number).IsEqualTo(PageNumber);
        await Assert.That(diagnostics.Exists(static item => item.Code == PdfDiagnosticCode.RecursionLimit)).IsTrue();
    }

    /// <summary>A ten thousand-node Kids chain stops and retains a valid root sibling.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Read_DeepKids_KeepsValidSibling()
    {
        var objects = new string[HostileDepth + PageNumber];
        objects[0] = Catalog;
        for (var index = 0; index < HostileDepth; index++)
        {
            var sibling = index == 0
                ? string.Create(CultureInfo.InvariantCulture, $" {HostileDepth + PageNumber} 0 R")
                : string.Empty;
            objects[index + 1] = string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Pages /Kids [{index + PageNumber} 0 R{sibling}] /Count 1 >>");
        }

        objects[HostileDepth + 1] = "<< /Type /Page >>";
        objects[HostileDepth + PageNumber - 1] = Page;
        var diagnostics = new List<PdfDiagnostic>();
        using var document = PdfDocumentReader.OpenWith(MiniPdf.Build(objects), new() { Diagnostics = diagnostics.Add });

        await Assert.That(document.PageCount).IsEqualTo(1);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Id.Number).IsEqualTo(HostileDepth + PageNumber);
        await Assert.That(diagnostics.Exists(static item => item.Code == PdfDiagnosticCode.RecursionLimit)).IsTrue();
    }

    /// <summary>Scanned pages keep inherited resources while a circular Parent chain terminates.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Read_CircularParents_InheritsNearestResources()
    {
        using var document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog >>",
                "<< /Type /Pages /Parent 5 0 R /Resources 4 0 R >>",
                Page,
                "<< /ProcSet [/PDF] >>",
                "<< /Type /Pages /Parent 2 0 R >>"), null);

        await Assert.That(document.PageCount).IsEqualTo(1);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Resources)
            .IsSameReferenceAs(StoreReading.GetDictionary(document.Objects, new(ResourceNumber, 0)));
    }

    /// <summary>Circular resource references resolve to null rather than recursing forever.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Read_CircularResourceReferences_UsesNoResources()
    {
        var diagnostics = new List<PdfDiagnostic>();
        using var document = PdfDocumentReader.OpenWith(
            MiniPdf.Build(
                Catalog,
                "<< /Type /Pages /Kids [3 0 R] /Count 1 /Resources 4 0 R >>",
                Page,
                "5 0 R",
                "4 0 R"),
            new() { Diagnostics = diagnostics.Add });

        await Assert.That(document.PageCount).IsEqualTo(1);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Resources).IsNull();
        await Assert.That(diagnostics.Exists(static item => item.Code == PdfDiagnosticCode.RecursionLimit)).IsTrue();
    }

    /// <summary>Scanned pages stop a ten thousand-object Parent chain before its remote resources.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Read_DeepParents_CutsRemoteResources()
    {
        var objects = new string[HostileDepth + PageNumber];
        objects[0] = "<< /Type /Catalog >>";
        objects[1] = "<< /Type /Pages /Parent 5 0 R >>";
        objects[PageNumber - 1] = Page;
        objects[ResourceNumber - 1] = "<< /ProcSet [/PDF] >>";
        for (var number = OtherParent; number <= objects.Length; number++)
        {
            objects[number - 1] = number == objects.Length
                ? "<< /Type /Pages /Resources 4 0 R >>"
                : string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Parent {number + 1} 0 R >>");
        }

        using var document = PdfDocumentReader.Open(MiniPdf.Build(objects), null);

        await Assert.That(document.PageCount).IsEqualTo(1);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Resources).IsNull();
    }
}
