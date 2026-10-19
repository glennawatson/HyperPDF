// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Checks FENote subtypes defined by PDF/UA-2 and WTPDF.</summary>
public sealed class NoteTypeReadingTests
{
    /// <summary>The namespace name used for namespace-owned FENote attributes.</summary>
    private const string NoteNamespace = "(FENote)";

    /// <summary>An explicit footnote attribute object.</summary>
    private const string FootnoteAttributes = "<< /O /FENote /NoteType /Footnote >>";

    /// <summary>An explicit endnote attribute object.</summary>
    private const string EndnoteAttributes = "<< /O /FENote /NoteType /Endnote >>";

    /// <summary>An explicitly unspecified note subtype.</summary>
    private const string UnspecifiedAttributes = "<< /O /FENote /NoteType /None >>";

    /// <summary>The edited title used to force an object revision.</summary>
    private const string SavedTitle = "saved note";

    /// <summary>Gets the edited title's PDFDocEncoding bytes.</summary>
    private static ReadOnlySpan<byte> SavedTitleBytes => "saved note"u8;

    /// <summary>Reads recognised subtypes and recovers absent or malformed values to None.</summary>
    /// <param name="entries">The NoteType entry, if present.</param>
    /// <param name="expected">The interpreted subtype.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("/NoteType /Footnote", PdfNoteType.Footnote)]
    [Arguments("/NoteType /Endnote", PdfNoteType.Endnote)]
    [Arguments("/NoteType /None", PdfNoteType.None)]
    [Arguments("", PdfNoteType.None)]
    [Arguments("/NoteType /Private", PdfNoteType.None)]
    [Arguments("/NoteType (Footnote)", PdfNoteType.None)]
    [Arguments("/NoteType null", PdfNoteType.None)]
    public async Task ReadsDeclaredNoteType(string entries, PdfNoteType expected, CancellationToken cancellationToken)
    {
        var bytes = Build($"<< /O /FENote {entries} >>", string.Empty, string.Empty, NoteNamespace);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        await Assert.That(note.Type).IsEqualTo(PdfStructureType.FootnoteOrEndnote);
        await Assert.That(note.NoteType).IsEqualTo(expected);
    }

    /// <summary>Ignores unrelated owners while recovering a missing owner leniently.</summary>
    /// <param name="owner">The owner entry, if present.</param>
    /// <param name="expected">The interpreted subtype.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("/O /FENote", PdfNoteType.Footnote)]
    [Arguments("/O /Layout", PdfNoteType.None)]
    [Arguments("/O /Private", PdfNoteType.None)]
    [Arguments("", PdfNoteType.Footnote)]
    public async Task NoteTypeRespectsItsOwner(string owner, PdfNoteType expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(
            Build($"<< {owner} /NoteType /Footnote >>", string.Empty, string.Empty, NoteNamespace),
            null,
            cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        await Assert.That(note.NoteType).IsEqualTo(expected);
    }

    /// <summary>Recovers namespace-owned note types without assuming a byte encoding.</summary>
    /// <param name="namespaceValue">The namespace text string.</param>
    /// <param name="expected">The interpreted subtype.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(NoteNamespace, PdfNoteType.Footnote)]
    [Arguments("<FEFF00460045004E006F00740065>", PdfNoteType.Footnote)]
    [Arguments("<FFFE460045004E006F0074006500>", PdfNoteType.Footnote)]
    [Arguments("<EFBBBF46454E6F7465>", PdfNoteType.Footnote)]
    [Arguments("(Private)", PdfNoteType.None)]
    [Arguments("(FENotes)", PdfNoteType.None)]
    public async Task ReadsNamespaceOwnedNoteType(string namespaceValue, PdfNoteType expected, CancellationToken cancellationToken)
    {
        var bytes = Build("<< /O /NSO /NS 7 0 R /NoteType /Footnote >>", string.Empty, string.Empty, namespaceValue);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        await Assert.That(note.NoteType).IsEqualTo(expected);
    }

    /// <summary>The last note attribute wins, including an explicit None.</summary>
    /// <param name="last">The last attribute object.</param>
    /// <param name="expected">The interpreted subtype.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(EndnoteAttributes, PdfNoteType.Endnote)]
    [Arguments(UnspecifiedAttributes, PdfNoteType.None)]
    [Arguments($"{EndnoteAttributes} 1", PdfNoteType.Endnote)]
    public async Task LaterNoteAttributesTakePrecedence(string last, PdfNoteType expected, CancellationToken cancellationToken)
    {
        var bytes = Build($"[{FootnoteAttributes} {last}]", string.Empty, string.Empty, NoteNamespace);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        await Assert.That(note.NoteType).IsEqualTo(expected);
    }

    /// <summary>Own note attributes override class attributes, including an explicit None.</summary>
    /// <param name="own">The element's own attributes.</param>
    /// <param name="expected">The interpreted subtype.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("null", PdfNoteType.Endnote)]
    [Arguments(FootnoteAttributes, PdfNoteType.Footnote)]
    [Arguments(UnspecifiedAttributes, PdfNoteType.None)]
    public async Task OwnNoteAttributesOverrideClasses(string own, PdfNoteType expected, CancellationToken cancellationToken)
    {
        const string classMap = $"/ClassMap << /First {FootnoteAttributes} /Last {EndnoteAttributes} >>";
        var bytes = Build(own, "/C [/First /Last]", classMap, NoteNamespace);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        await Assert.That(note.NoteType).IsEqualTo(expected);
    }

    /// <summary>An absent child subtype does not inherit its ancestor's subtype.</summary>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task NoteTypeDoesNotInherit(CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Version /2.0 /Pages 2 0 R /StructTreeRoot 3 0 R >>",
            "<< /Type /Pages /Count 0 /Kids [] >>",
            "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [7 0 R] >>",
            "<< /Type /StructElem /S /Document /P 3 0 R /NS 7 0 R /K 5 0 R >>",
            $"<< /Type /StructElem /S /FENote /P 4 0 R /NS 7 0 R /K 6 0 R /A {FootnoteAttributes} >>",
            "<< /Type /StructElem /S /P /P 5 0 R /NS 7 0 R >>",
            "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        await Assert.That(note.NoteType).IsEqualTo(PdfNoteType.Footnote);
        await Assert.That(note.Kids[0].Element!.NoteType).IsEqualTo(PdfNoteType.None);
    }

    /// <summary>Both save formats preserve valid note subtypes when the note element is edited.</summary>
    /// <param name="incremental">Whether to append an object revision.</param>
    /// <param name="type">The source subtype name.</param>
    /// <param name="expected">The interpreted subtype after reopening.</param>
    /// <param name="cancellationToken">Cancels saving and reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(false, nameof(PdfNoteType.Footnote), PdfNoteType.Footnote)]
    [Arguments(true, nameof(PdfNoteType.Footnote), PdfNoteType.Footnote)]
    [Arguments(false, nameof(PdfNoteType.Endnote), PdfNoteType.Endnote)]
    [Arguments(true, nameof(PdfNoteType.Endnote), PdfNoteType.Endnote)]
    [Arguments(false, nameof(PdfNoteType.None), PdfNoteType.None)]
    [Arguments(true, nameof(PdfNoteType.None), PdfNoteType.None)]
    public async Task SavesPreserveNoteType(bool incremental, string type, PdfNoteType expected, CancellationToken cancellationToken)
    {
        var bytes = Build($"<< /O /FENote /NoteType /{type} >>", string.Empty, string.Empty, NoteNamespace);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        var edited = note.Dictionary.Clone();
        edited.Set(KnownName.T, PdfValue.FromString(SavedTitleBytes.ToArray()));
        StoreEditing.Replace(document.Objects, note.Id, PdfValue.FromDictionary(edited));
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
        var rawType = saved.Dictionary.GetDictionary(KnownName.A)!.Get(names.Intern("NoteType"u8)).AsName();
        await Assert.That(saved.Title).IsEqualTo(SavedTitle);
        await Assert.That(saved.NoteType).IsEqualTo(expected);
        await Assert.That(names.GetString(rawType)).IsEqualTo(type);
    }

    /// <summary>Builds a PDF 2.0 structure tree containing one FENote element.</summary>
    /// <param name="attributes">The note's A entry value.</param>
    /// <param name="classes">The optional C entry.</param>
    /// <param name="classMap">The optional root class map.</param>
    /// <param name="namespaceValue">The attribute owner's namespace text string.</param>
    /// <returns>The input bytes.</returns>
    private static byte[] Build(string attributes, string classes, string classMap, string namespaceValue) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Version /2.0 /Pages 2 0 R /StructTreeRoot 3 0 R >>",
        "<< /Type /Pages /Count 0 /Kids [] >>",
        $"<< /Type /StructTreeRoot /K 4 0 R /Namespaces [6 0 R] {classMap} >>",
        "<< /Type /StructElem /S /Document /P 3 0 R /NS 6 0 R /K 5 0 R >>",
        $"<< /Type /StructElem /S /FENote /P 4 0 R /NS 6 0 R /A {attributes} {classes} >>",
        "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        $"<< /Type /Namespace /NS {namespaceValue} >>",
    ]);
}
