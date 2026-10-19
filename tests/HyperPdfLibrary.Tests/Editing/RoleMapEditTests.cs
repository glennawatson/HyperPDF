// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Checks that role map entries can be added, replaced and removed, that bad ones are refused, and that undo restores the map.</summary>
public sealed class RoleMapEditTests
{
    /// <summary>The custom type the tags in the test file use.</summary>
    private const string Custom = "DataTable";

    /// <summary>A custom type that no tag uses.</summary>
    private const string Unused = "Sidebar";

    /// <summary>The number of mappings after the two custom types are mapped.</summary>
    private const int Two = 2;

    /// <summary>A mapping is added, read back, replaced, saved and undone, and the tag then resolves to the standard type.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MappingIsAddedReplacedAndUndone(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(string.Empty), null, cancellationToken);
        await Assert.That(PdfDocumentStructureEditing.GetRoleMap(document).Length == 0).IsTrue();

        PdfDocumentStructureEditing.SetRoleMapping(document, Custom, "Table");
        PdfDocumentStructureEditing.SetRoleMapping(document, Unused, "Sect");
        PdfDocumentStructureEditing.SetRoleMapping(document, Unused, "Div");

        var map = PdfDocumentStructureEditing.GetRoleMap(document);
        using var reopened = PdfDocumentReader.Open(EditChecks.SaveCompact(document), null);
        var saved = PdfDocumentStructureEditing.GetRoleMap(reopened);
        using (Assert.Multiple())
        {
            await Assert.That(map.Length).IsEqualTo(Two);
            await Assert.That(map.Single(static m => m.Type == Unused).MapsTo).IsEqualTo("Div");
            await Assert.That(saved.Length).IsEqualTo(Two);
            await Assert.That(PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.MappedType).IsEqualTo("Table");
        }

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentStructureEditing.GetRoleMap(document).Single(static m => m.Type == Unused).MapsTo).IsEqualTo("Sect");
    }

    /// <summary>A standard type cannot be mapped, the target must be standard, and a cycle is refused.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadMappingsAreRefused(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(string.Empty), null, cancellationToken);
        PdfDocumentStructureEditing.SetRoleMapping(document, Unused, "Div");

        using (Assert.Multiple())
        {
            await Assert.That(() => PdfDocumentStructureEditing.SetRoleMapping(document, "P", "Div")).Throws<ArgumentException>();
            await Assert.That(() => PdfDocumentStructureEditing.SetRoleMapping(document, Custom, "NotAType")).Throws<ArgumentException>();
            await Assert.That(() => PdfDocumentStructureEditing.SetRoleMapping(document, "Has Space", "Div")).Throws<ArgumentException>();
            await Assert.That(PdfDocumentStructureEditing.GetRoleMap(document).Length).IsEqualTo(1);
        }
    }

    /// <summary>A mapping that tags still use is kept; an unused one is removed.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyUnusedMappingsAreRemoved(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("/RoleMap << /DataTable /Table /Sidebar /Div >>"), null, cancellationToken);

        var inUse = Assert.Throws<InvalidOperationException>(() => PdfDocumentStructureEditing.RemoveRoleMapping(document, Custom));
        var removed = PdfDocumentStructureEditing.RemoveRoleMapping(document, Unused);
        var again = PdfDocumentStructureEditing.RemoveRoleMapping(document, Unused);

        using (Assert.Multiple())
        {
            await Assert.That(inUse.Message).Contains(Custom);
            await Assert.That(removed).IsTrue();
            await Assert.That(again).IsFalse();
            await Assert.That(PdfDocumentStructureEditing.GetRoleMap(document).Single().Type).IsEqualTo(Custom);
        }
    }

    /// <summary>Builds a document with one tag of a custom type.</summary>
    /// <param name="roleMap">The root's role map entry, or empty.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string roleMap) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
        "<< /Type /StructElem /S /Document /P 5 0 R /K [6 0 R] >>",
        $"<< /Type /StructTreeRoot /K 4 0 R {roleMap} >>",
        $"<< /Type /StructElem /S /{Custom} /P 4 0 R /Pg 3 0 R /K 0 >>",
    ]);
}
