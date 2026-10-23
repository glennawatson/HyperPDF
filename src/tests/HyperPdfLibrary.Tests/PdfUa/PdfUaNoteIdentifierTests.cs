// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Tests.Accessibility;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.PdfUa;

/// <summary>Checks claimed part 1 saves without guessing or rewriting Note identifiers.</summary>
public sealed class PdfUaNoteIdentifierTests
{
    /// <summary>The title edit used to force an incremental revision.</summary>
    private const string EditedTitle = "Edited";

    /// <summary>Both writing modes refuse missing or duplicate IDs before writing output.</summary>
    /// <param name="incremental">Whether to save incrementally.</param>
    /// <param name="duplicate">Whether the ID is repeated instead of missing.</param>
    /// <param name="cancellationToken">Cancels saving.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task InvalidIdentifierRefusesClaimedSave(bool incremental, bool duplicate, CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(Build(duplicate), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();
        var caught = await Assert.That(async () => await SaveAsync(document, output, report, incremental, cancellationToken)).Throws<PdfException>();
        var error = await Assert.That(caught).IsNotNull();
        await Assert.That(error.Error).IsEqualTo(PdfError.ClaimUnmet);
        await Assert.That(error.Message).Contains("Clause 7.9");
        await Assert.That(output.Length).IsEqualTo(0);
        await Assert.That(report.UaClaim!.GetUnrepaired().Select(static finding => finding.Requirement)).Contains(PdfUaRequirement.NoteIdentifier);
    }

    /// <summary>An explicit claim removal preserves the Note data in both writing modes.</summary>
    /// <param name="incremental">Whether to save incrementally.</param>
    /// <param name="duplicate">Whether the ID is repeated instead of missing.</param>
    /// <param name="cancellationToken">Cancels saving and reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task RemovingClaimPreservesNoteIdentifier(bool incremental, bool duplicate, CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(Build(duplicate), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var report = new PdfSaveReport { UnmetUaClaim = PdfUaUnmetClaimAction.RemoveClaim };
        await using var output = new MemoryStream();
        await SaveAsync(document, output, report, incremental, cancellationToken);
        await Assert.That(report.UaClaim!.Removed).IsTrue();
        using var saved = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(saved)!.Roots[0].Kids[0].Element!;
        await Assert.That(note.Dictionary.Get(KnownName.ID).Kind).IsEqualTo(duplicate ? PdfKind.String : PdfKind.Null);
        await Assert.That(note.ElementId).IsEqualTo(duplicate ? "same" : null);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(saved, cancellationToken);
        await Assert.That(accessibility.Claim.Part).IsNotEqualTo(PdfUaClaim.Part1);
    }

    /// <summary>A unique Note ID and its ID-tree entry survive both writing modes without removing the claim.</summary>
    /// <param name="incremental">Whether to save incrementally.</param>
    /// <param name="cancellationToken">Cancels saving and reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UniqueIdentifierPreservesClaim(bool incremental, CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(AccessibilityPdfs.Build(new() { Layout = static (pdf, parent) => AccessibilityPdfs.Kids(pdf.Element("Note", parent, "/K 0 /ID (note)")) }), null);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var note = tree.Roots[0].Kids[0].Element!;
        var names = new PdfArray(document.Objects);
        names.Add(PdfValue.FromString("note"u8.ToArray()));
        names.Add(PdfValue.FromReference(note.Id));
        var idTree = new PdfDictionary(document.Objects);
        idTree.Set(KnownName.Names, PdfValue.FromArray(names));
        var root = tree.Root.Clone();
        root.Set(document.Objects.Names.Intern("IDTree"u8), PdfValue.FromDictionary(idTree));
        StoreEditing.Replace(document.Objects, document.Catalog.GetRaw(KnownName.StructTreeRoot).AsReference(), PdfValue.FromDictionary(root));
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();
        await SaveAsync(document, output, report, incremental, cancellationToken);
        var claim = await Assert.That(report.UaClaim).IsNotNull();
        await Assert.That(claim.Removed).IsFalse();
        await Assert.That(claim.GetUnrepaired()).IsEmpty();
        using var saved = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var savedTree = PdfDocumentTagged.GetStructureTree(saved)!;
        var savedNote = savedTree.Roots[0].Kids[0].Element!;
        await Assert.That(savedNote.Dictionary.Get(KnownName.ID).AsStringBytes().SequenceEqual("note"u8)).IsTrue();
        await Assert.That(savedTree.FindById("note")).IsSameReferenceAs(savedNote);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(saved, cancellationToken);
        await Assert.That(accessibility.Claim.Part).IsEqualTo(PdfUaClaim.Part1);
        await Assert.That(accessibility.GetCount(PdfAccessibilityCode.NoteIdentifierInvalid)).IsEqualTo(0);
    }

    /// <summary>Builds a Note with either no ID or an ID shared with a paragraph.</summary>
    /// <param name="duplicate">Whether to repeat the identifier.</param>
    /// <returns>The source bytes.</returns>
    private static byte[] Build(bool duplicate) => AccessibilityPdfs.Build(new()
    {
        Layout = (pdf, parent) => AccessibilityPdfs.Kids(
            pdf.Element("Note", parent, duplicate ? "/K 0 /ID (same)" : "/K 0"),
            pdf.Element("P", parent, "/ID (same)")),
    });

    /// <summary>Saves through the selected writing mode.</summary>
    /// <param name="document">The edited document.</param>
    /// <param name="output">Receives saved bytes.</param>
    /// <param name="report">Receives claim decisions.</param>
    /// <param name="incremental">Whether to save incrementally.</param>
    /// <param name="cancellationToken">Cancels writing.</param>
    /// <returns>A task.</returns>
    private static Task SaveAsync(PdfDocument document, MemoryStream output, PdfSaveReport report, bool incremental, CancellationToken cancellationToken) =>
        incremental
            ? PdfIncrementalWriter.SaveAsync(document.Objects, output, report, cancellationToken)
            : PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, report, cancellationToken);
}
