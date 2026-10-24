// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Checks hostile xref stream headers at the strict document-open boundary.</summary>
public sealed class XrefStreamBoundsTests
{
    /// <summary>The catalog object number.</summary>
    private const int CatalogNumber = 1;

    /// <summary>The xref stream object number.</summary>
    private const int XrefNumber = 2;

    /// <summary>The number of object slots, including the free object.</summary>
    private const int ObjectSlots = 3;

    /// <summary>The valid widths of the type, file offset and generation fields.</summary>
    private const string ValidWidths = "/W [1 4 2]";

    /// <summary>The subsection selecting one catalog entry.</summary>
    private const string CatalogIndex = "[1 1]";

    /// <summary>The crafted file is otherwise readable, so invalid-header tests isolate the corrupt value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrictOpenReadsValidStreamHeader()
    {
        var file = FileWithHeader(ValidWidths, CatalogIndex, "7");
        using var store = StoreOpening.OpenWith(file, new PdfOpenOptions { Recovery = false });
        await Assert.That(store.Catalog.GetName(KnownName.Type).Is(KnownName.Catalog)).IsTrue();
    }

    /// <summary>A stream header whose index cannot address the catalog produces a PDF format error.</summary>
    /// <param name="first">The first indexed object.</param>
    /// <param name="count">The announced entry count.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(int.MaxValue, 1)]
    [Arguments(-1, 1)]
    [Arguments(0, -1)]
    [Arguments(int.MaxValue, int.MaxValue)]
    public async Task StrictOpenRejectsOutOfRangeIndex(int first, int count)
    {
        var index = string.Create(CultureInfo.InvariantCulture, $"[{first} {count}]");
        var file = FileWithHeader(ValidWidths, index, "7");
        await Assert.That(() => OpenStrict(file)).Throws<PdfException>();
    }

    /// <summary>Widths cannot overflow an entry length before a strict open rejects the unreadable table.</summary>
    /// <param name="widths">The announced field widths.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("[2147483647 2147483647 2147483647]")]
    [Arguments("[-1 -1 -1]")]
    public async Task StrictOpenBoundsFieldWidths(string widths)
    {
        var file = FileWithHeader($"/W {widths}", CatalogIndex, "7");
        await Assert.That(() => OpenStrict(file)).Throws<PdfException>();
    }

    /// <summary>Negative and maximum stream lengths never escape as runtime slicing exceptions.</summary>
    /// <param name="length">The announced byte count.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("-1")]
    [Arguments("2147483647")]
    public async Task StrictOpenBoundsStreamLength(string length)
    {
        var file = FileWithHeader(ValidWidths, CatalogIndex, length);
        await Assert.That(() => OpenStrict(file)).Throws<PdfException>();
    }

    /// <summary>Writes a catalog and a crafted xref stream header without normalizing it.</summary>
    /// <param name="widths">The /W entry.</param>
    /// <param name="index">The /Index array.</param>
    /// <param name="length">The raw /Length number.</param>
    /// <returns>The file.</returns>
    private static byte[] FileWithHeader(string widths, string index, string length)
    {
        var pdf = new RawPdf();
        var catalogOffset = pdf.Position;
        _ = pdf.Object(CatalogNumber, "<< /Type /Catalog /Pages << /Type /Pages /Kids [] /Count 0 >> >>");
        var offset = pdf.Position;
        _ = pdf.Append(string.Create(
            CultureInfo.InvariantCulture,
            $"{XrefNumber} 0 obj\n<< /Type /XRef /Size {ObjectSlots} /Root {CatalogNumber} 0 R {widths} /Index {index} /Length {length} >>\nstream\n"));
        _ = pdf.Append([1, 0, 0, 0, (byte)catalogOffset, 0, 0]);
        _ = pdf.Append(string.Create(CultureInfo.InvariantCulture, $"\nendstream\nendobj\nstartxref\n{offset}\n%%EOF\n"));
        return pdf.ToArray();
    }

    /// <summary>Opens without rebuilding malformed cross-reference data.</summary>
    /// <param name="file">The PDF bytes.</param>
    private static void OpenStrict(byte[] file)
    {
        using var store = StoreOpening.OpenWith(file, new PdfOpenOptions { Recovery = false });
    }
}
