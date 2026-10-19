// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Checks structure attribute ownership, precedence and complete source values.</summary>
public sealed class StructureAttributeReaderTests
{
    /// <summary>The number of header associations beyond the former reader limit.</summary>
    private const int HeaderCount = 257;

    /// <summary>A table span beyond the former reader ceiling.</summary>
    private const int LargeSpan = 1001;

    /// <summary>The non-default span used to distinguish attribute owners.</summary>
    private const int OwnedSpan = 7;

    /// <summary>The title used to force an edited structure element revision.</summary>
    private const string SavedCellTitle = "saved cell";

    /// <summary>Gets the title's PDFDocEncoding bytes.</summary>
    private static ReadOnlySpan<byte> SavedCellTitleBytes => "saved cell"u8;

    /// <summary>Standard attributes follow their owner restrictions, including permitted private list owners.</summary>
    /// <param name="owner">The owner name.</param>
    /// <param name="namespaceName">The namespace name for an NSO owner.</param>
    /// <param name="layout">Whether Layout attributes apply.</param>
    /// <param name="table">Whether Table attributes apply.</param>
    /// <param name="list">Whether List attributes apply.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("Layout", "", true, false, false)]
    [Arguments("Table", "", false, true, false)]
    [Arguments("List", "", false, false, true)]
    [Arguments("Private", "", false, false, true)]
    [Arguments("FENote", "", false, false, true)]
    [Arguments("PrintField", "", false, false, false)]
    [Arguments("Artifact", "", false, false, false)]
    [Arguments("NSO", "Layout", true, false, false)]
    [Arguments("NSO", "Table", false, true, false)]
    [Arguments("NSO", "List", false, false, true)]
    [Arguments("NSO", "Private", false, false, true)]
    [Arguments("NSO", "PrintField", false, false, false)]
    [Arguments("NSO", "Artifact", false, false, false)]
    [Arguments("NSO", "\\376\\377\\000T\\000a\\000b\\000l\\000e", false, true, false)]
    [Arguments("NSO", "\\377\\376T\\000a\\000b\\000l\\000e\\000", false, true, false)]
    [Arguments("NSO", "\\357\\273\\277Table", false, true, false)]
    public async Task AttributesRespectTheirOwner(string owner, string namespaceName, bool layout, bool table, bool list, CancellationToken cancellationToken)
    {
        var ownerEntries = owner == "NSO" ? "/O /NSO /NS 6 0 R" : $"/O /{owner}";
        var attributes = $"<< {ownerEntries} /Placement /Block /BBox [0 0 1 1] /Scope /Both /Headers [(header)] /RowSpan 7 /ColSpan 7 /ListNumbering /Decimal /Summary (table) >>";
        using var document = await PdfDocumentReader.OpenAsync(BuildAttributes(attributes, string.Empty, string.Empty, namespaceName), null, cancellationToken);
        var read = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.Attributes;
        await Assert.That(read.Placement).IsEqualTo(layout ? "Block" : string.Empty);
        await Assert.That(read.BoundingBox.HasValue).IsEqualTo(layout);
        await Assert.That(read.Scope).IsEqualTo(table ? PdfTableScope.Both : PdfTableScope.None);
        await Assert.That(read.Headers.Length).IsEqualTo(table ? 1 : 0);
        await Assert.That(read.RowSpan).IsEqualTo(table ? OwnedSpan : 1);
        await Assert.That(read.ColumnSpan).IsEqualTo(table ? OwnedSpan : 1);
        await Assert.That(read.ListNumbering).IsEqualTo(list ? "Decimal" : string.Empty);
        await Assert.That(read.Summary).IsEqualTo(table ? "table" : string.Empty);
    }

    /// <summary>The last attribute object overrides earlier values belonging to the same owner.</summary>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task LaterAttributeObjectsTakePrecedence(CancellationToken cancellationToken)
    {
        var bytes = BuildAttributes("[<< /O /Table /RowSpan 3 >> << /O /Table /RowSpan 7 >>]", string.Empty, string.Empty, string.Empty);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var attributes = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.Attributes;
        await Assert.That(attributes.RowSpan).IsEqualTo(OwnedSpan);
    }

    /// <summary>Own attributes override classes, while later class names override earlier names.</summary>
    /// <param name="own">The element's own attribute object.</param>
    /// <param name="expected">The effective row span.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("null", 7)]
    [Arguments("<< /O /Table /RowSpan 3 >>", 3)]
    public async Task OwnAttributesOverrideOrderedClasses(string own, int expected, CancellationToken cancellationToken)
    {
        var bytes = BuildAttributes(own, "/C [/First /Last]", "/ClassMap << /First << /O /Table /RowSpan 3 >> /Last << /O /Table /RowSpan 7 >> >>", string.Empty);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var attributes = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.Attributes;
        await Assert.That(attributes.RowSpan).IsEqualTo(expected);
    }

    /// <summary>Recovers standard-looking attributes whose required owner is omitted.</summary>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task MissingOwnerIsReadLeniently(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(BuildAttributes("<< /RowSpan 7 >>", string.Empty, string.Empty, string.Empty), null, cancellationToken);
        var attributes = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.Attributes;
        await Assert.That(attributes.RowSpan).IsEqualTo(OwnedSpan);
    }

    /// <summary>Saves preserve a namespace-owned attribute object when its element is edited.</summary>
    /// <param name="incremental">Whether to append an object revision.</param>
    /// <param name="cancellationToken">Cancels saving and reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SavesPreserveNamespaceOwnedAttributes(bool incremental, CancellationToken cancellationToken)
    {
        var bytes = BuildAttributes("<< /O /NSO /NS 6 0 R /RowSpan 7 >>", string.Empty, string.Empty, "Table");
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var cell = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        var edited = cell.Dictionary.Clone();
        edited.Set(KnownName.T, PdfValue.FromString(SavedCellTitleBytes.ToArray()));
        StoreEditing.Replace(document.Objects, cell.Id, PdfValue.FromDictionary(edited));
        await using var output = new MemoryStream();
        var report = new PdfSaveReport();
        if (incremental)
        {
            await PdfIncrementalWriter.SaveAsync(document.Objects, output, report, cancellationToken);
        }
        else
        {
            await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, report, cancellationToken);
        }

        using var reopened = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var saved = PdfDocumentTagged.GetStructureTree(reopened)!.Roots[0].Kids[0].Element!;
        var names = reopened.Objects.Names;
        var rawAttributes = saved.Dictionary.GetDictionary(KnownName.A)!;
        var ns = names.Intern("NS"u8);
        await Assert.That(saved.Title).IsEqualTo(SavedCellTitle);
        await Assert.That(saved.Attributes.RowSpan).IsEqualTo(OwnedSpan);
        await Assert.That(names.NameEquals(rawAttributes.Get(KnownName.O).AsName(), "NSO"u8)).IsTrue();
        await Assert.That(rawAttributes.GetDictionary(ns)!.GetText(ns)).IsEqualTo("Table");
    }

    /// <summary>Reads the complete positive integer span rather than restricting it to a small table.</summary>
    /// <param name="span">The declared row and column span.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(1001)]
    [Arguments(int.MaxValue)]
    public async Task ReadsCompleteTableSpans(int span, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build($" /RowSpan {span} /ColSpan {span}"), null, cancellationToken);
        var attributes = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.Attributes;

        await Assert.That(attributes.RowSpan).IsEqualTo(span);
        await Assert.That(attributes.ColumnSpan).IsEqualTo(span);
    }

    /// <summary>Reads header associations beyond the first 256 entries in their original order.</summary>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ReadsEveryHeaderAssociation(CancellationToken cancellationToken)
    {
        var source = new StringBuilder(" /Headers [");
        var expected = new string[HeaderCount];
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = string.Create(CultureInfo.InvariantCulture, $"header-{i}");
            _ = source.Append('(').Append(expected[i]).Append(") ");
        }

        _ = source.Append(']');
        using var document = await PdfDocumentReader.OpenAsync(Build(source.ToString()), null, cancellationToken);
        var attributes = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.Attributes;

        await Assert.That(attributes.Headers.Length).IsEqualTo(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            await Assert.That(attributes.Headers[i]).IsEqualTo(expected[i]);
        }
    }

    /// <summary>Skips malformed header entries while retaining later byte strings in order.</summary>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task MalformedHeaderEntriesDoNotHideLaterAssociations(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(" /Headers [null 1 /Invalid (first) <<>> (second)]"), null, cancellationToken);
        var attributes = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.Attributes;

        await Assert.That(attributes.Headers).IsEquivalentTo(["first", "second"]);
        await Assert.That(attributes.Headers[0]).IsEqualTo("first");
        await Assert.That(attributes.Headers[1]).IsEqualTo("second");
    }

    /// <summary>Both save formats retain complete table spans and header associations.</summary>
    /// <param name="incremental">Whether to append a revision instead of compacting the file.</param>
    /// <param name="cancellationToken">Cancels saving and reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SavesPreserveCompleteTableAttributes(bool incremental, CancellationToken cancellationToken)
    {
        var headers = new StringBuilder();
        for (var i = 0; i < HeaderCount; i++)
        {
            _ = headers.Append(CultureInfo.InvariantCulture, $"(header-{i}) ");
        }

        using var document = await PdfDocumentReader.OpenAsync(Build($" /RowSpan {LargeSpan} /ColSpan {LargeSpan} /Headers [{headers}]"), null, cancellationToken);
        var cell = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        var edited = cell.Dictionary.Clone();
        edited.Set(KnownName.T, PdfValue.FromString(SavedCellTitleBytes.ToArray()));
        StoreEditing.Replace(document.Objects, cell.Id, PdfValue.FromDictionary(edited));
        await using var output = new MemoryStream();
        var report = new PdfSaveReport();
        if (incremental)
        {
            await PdfIncrementalWriter.SaveAsync(document.Objects, output, report, cancellationToken);
        }
        else
        {
            await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, report, cancellationToken);
        }

        using var reopened = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var savedCell = PdfDocumentTagged.GetStructureTree(reopened)!.Roots[0].Kids[0].Element!;
        var attributes = savedCell.Attributes;
        await Assert.That(savedCell.Title).IsEqualTo(SavedCellTitle);
        await Assert.That(attributes.RowSpan).IsEqualTo(LargeSpan);
        await Assert.That(attributes.ColumnSpan).IsEqualTo(LargeSpan);
        await Assert.That(attributes.Headers.Length).IsEqualTo(HeaderCount);
        for (var i = 0; i < HeaderCount; i++)
        {
            await Assert.That(attributes.Headers[i]).IsEqualTo(string.Create(CultureInfo.InvariantCulture, $"header-{i}"));
        }
    }

    /// <summary>Uses default spans when malformed values are encountered.</summary>
    /// <param name="span">The malformed span spelling.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("0")]
    [Arguments("-1")]
    [Arguments("(bad)")]
    public async Task InvalidSpansUseTheDefault(string span, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build($" /RowSpan {span} /ColSpan {span}"), null, cancellationToken);
        var attributes = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.Attributes;

        await Assert.That(attributes.RowSpan).IsEqualTo(1);
        await Assert.That(attributes.ColumnSpan).IsEqualTo(1);
    }

    /// <summary>Builds a cell with the supplied Table attribute entries.</summary>
    /// <param name="entries">The attribute entries.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Build(string entries) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>",
        "<< /Type /Pages /Count 0 /Kids [] >>",
        "<< /Type /StructTreeRoot /K 4 0 R >>",
        "<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R >>",
        $"<< /Type /StructElem /S /TD /P 4 0 R /A << /O /Table {entries} >> >>",
    ]);

    /// <summary>Builds an element with arbitrary attribute objects, classes and a namespace dictionary.</summary>
    /// <param name="attributes">The A entry value.</param>
    /// <param name="classes">The optional C entry.</param>
    /// <param name="classMap">The optional root class map.</param>
    /// <param name="namespaceName">The attribute namespace name.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] BuildAttributes(string attributes, string classes, string classMap, string namespaceName) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>",
        "<< /Type /Pages /Count 0 /Kids [] >>",
        $"<< /Type /StructTreeRoot /K 4 0 R {classMap} >>",
        "<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R >>",
        $"<< /Type /StructElem /S /TD /P 4 0 R /A {attributes} {classes} >>",
        $"<< /Type /Namespace /NS ({namespaceName}) >>",
    ]);
}
