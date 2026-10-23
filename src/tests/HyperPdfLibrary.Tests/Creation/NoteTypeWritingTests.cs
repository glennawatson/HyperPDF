// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Conformance.VeraPdf;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Checks optional FENote subtypes written under their own attribute owner.</summary>
public sealed class NoteTypeWritingTests
{
    /// <summary>The standard footnote and endnote structure type.</summary>
    private const string FootnoteType = "FENote";

    /// <summary>The owner of table attributes.</summary>
    private const string TableOwner = "Table";

    /// <summary>The optional subtype key.</summary>
    private const string NoteTypeKey = "NoteType";

    /// <summary>A value outside the defined subtypes and table scopes.</summary>
    private const int UndefinedValue = 99;

    /// <summary>The number of independently owned attribute objects.</summary>
    private const int AttributeOwnerCount = 2;

    /// <summary>The side length of a blank test page.</summary>
    private const float PageSide = 300F;

    /// <summary>The explicit merged-cell span.</summary>
    private const int CellSpan = 2;

    /// <summary>The independently checked note clause.</summary>
    private const string NoteClause = "8.2.5.14";

    /// <summary>The subtype value rule within the note clause.</summary>
    private const string SubtypeRule = "4";

    /// <summary>Explicit subtypes are names in an FENote-owned attribute object and read back unchanged.</summary>
    /// <param name="type">The requested subtype.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfNoteType.None)]
    [Arguments(PdfNoteType.Footnote)]
    [Arguments(PdfNoteType.Endnote)]
    public async Task ExplicitSubtypeRoundTrips(PdfNoteType type, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateNoteBytes(new() { NoteType = type }), null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        var attributes = note.Dictionary.GetDictionary(KnownName.A);

        await Assert.That(attributes).IsNotNull();
        await Assert.That(document.Objects.Names.GetString(attributes!.Get(KnownName.O).AsName())).IsEqualTo(FootnoteType);
        await Assert.That(attributes.Get(document.Objects.Names.Intern(NoteTypeKey)).Kind).IsEqualTo(PdfKind.Name);
        await Assert.That(document.Objects.Names.GetString(attributes.Get(document.Objects.Names.Intern(NoteTypeKey)).AsName())).IsEqualTo(type.ToString());
        await Assert.That(note.NoteType).IsEqualTo(type);
    }

    /// <summary>An omitted subtype leaves no attribute object and reads as the standard default.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingSubtypeStaysOptional(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateNoteBytes(PdfTagOptions.Default), null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;

        await Assert.That(note.Dictionary.Get(KnownName.A).Kind).IsEqualTo(PdfKind.Null);
        await Assert.That(note.NoteType).IsEqualTo(PdfNoteType.None);
    }

    /// <summary>Unknown subtype values cannot silently become an omitted attribute or a numeric name.</summary>
    /// <param name="value">The invalid enum value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(UndefinedValue)]
    public async Task UndefinedSubtypeIsRejected(int value)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder(), true);
        await Assert.That(() => tags.AddElement(tags.Root, FootnoteType, new() { NoteType = (PdfNoteType)value }))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>Unknown table scope values cannot be emitted as nonstandard names.</summary>
    /// <param name="value">The invalid enum value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(UndefinedValue)]
    public async Task UndefinedTableScopeIsRejected(int value)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder());
        await Assert.That(() => tags.AddElement(tags.Root, "TH", new() { TableScope = (PdfTableScope)value }))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>Multiple requested attribute owners keep independent objects instead of overwriting each other.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttributeOwnersStaySeparate(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(
            CreateNoteBytes(new() { NoteType = PdfNoteType.Footnote, TableScope = PdfTableScope.Row }),
            null,
            cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        var attributes = note.Dictionary.GetArray(KnownName.A);

        await Assert.That(attributes).IsNotNull();
        await Assert.That(attributes!.Count).IsEqualTo(AttributeOwnerCount);
        var owners = new HashSet<string>(StringComparer.Ordinal);
        PdfDictionary? table = null;
        for (var index = 0; index < attributes.Count; index++)
        {
            var attribute = attributes.Get(index).AsDictionary()!;
            var owner = document.Objects.Names.GetString(attribute.Get(KnownName.O).AsName());
            _ = owners.Add(owner);
            if (owner == TableOwner)
            {
                table = attribute;
            }
        }

        await Assert.That(owners.SetEquals([TableOwner, FootnoteType])).IsTrue();
        await Assert.That(note.NoteType).IsEqualTo(PdfNoteType.Footnote);
        await Assert.That(table).IsNotNull();
        await Assert.That(document.Objects.Names.GetString(table!.Get(document.Objects.Names.Intern("Scope")).AsName())).IsEqualTo("Row");
    }

    /// <summary>Table scopes and spans stay in a Table-owned dictionary without an optional note subtype.</summary>
    /// <param name="scope">The defined table scope.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfTableScope.None)]
    [Arguments(PdfTableScope.Row)]
    [Arguments(PdfTableScope.Column)]
    [Arguments(PdfTableScope.Both)]
    public async Task TableSpansRetainTheirValues(PdfTableScope scope, CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        _ = creator.Tags!.AddElement(
            creator.Tags.Root,
            "TH",
            new() { TableScope = scope, RowSpan = CellSpan, ColumnSpan = CellSpan });
        using var document = await PdfDocumentReader.OpenAsync(creator.ToArray(), null, cancellationToken);
        var header = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        var attributes = header.Dictionary.GetDictionary(KnownName.A)!;
        var names = document.Objects.Names;

        await Assert.That(names.GetString(attributes.Get(KnownName.O).AsName())).IsEqualTo(TableOwner);
        await Assert.That(attributes.Get(names.Intern("RowSpan")).AsInteger()).IsEqualTo(CellSpan);
        await Assert.That(attributes.Get(names.Intern("ColSpan")).AsInteger()).IsEqualTo(CellSpan);
        await Assert.That(attributes.Get(names.Intern(NoteTypeKey)).Kind).IsEqualTo(PdfKind.Null);
        var writtenScope = attributes.Get(names.Intern("Scope"));
        if (scope == PdfTableScope.None)
        {
            await Assert.That(writtenScope.Kind).IsEqualTo(PdfKind.Null);
        }
        else
        {
            await Assert.That(writtenScope.Kind).IsEqualTo(PdfKind.Name);
            await Assert.That(names.GetString(writtenScope.AsName())).IsEqualTo(scope.ToString());
        }
    }

    /// <summary>Editing an element retains its explicit subtype through either saved revision format.</summary>
    /// <param name="type">The explicit subtype to preserve.</param>
    /// <param name="incremental">Whether to append a revision.</param>
    /// <param name="cancellationToken">Cancels reading and saving.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfNoteType.None, false)]
    [Arguments(PdfNoteType.None, true)]
    [Arguments(PdfNoteType.Footnote, false)]
    [Arguments(PdfNoteType.Footnote, true)]
    [Arguments(PdfNoteType.Endnote, false)]
    [Arguments(PdfNoteType.Endnote, true)]
    public async Task EditedSubtypeRoundTrips(PdfNoteType type, bool incremental, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateNoteBytes(new() { NoteType = type }), null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        var edited = note.Dictionary.Clone();
        edited.Set(KnownName.T, PdfValue.FromString("edited note"u8.ToArray()));
        StoreEditing.Replace(document.Objects, note.Id, PdfValue.FromDictionary(edited));
        await using var output = new MemoryStream();
        if (incremental)
        {
            await PdfIncrementalWriter.SaveAsync(document.Objects, output, new(), cancellationToken);
        }
        else
        {
            await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, cancellationToken);
        }

        using var saved = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var savedNote = PdfDocumentTagged.GetStructureTree(saved)!.Roots[0].Kids[0].Element!;
        await Assert.That(savedNote.Title).IsEqualTo("edited note");
        await Assert.That(savedNote.NoteType).IsEqualTo(type);
        var attributes = savedNote.Dictionary.GetDictionary(KnownName.A);
        await Assert.That(attributes).IsNotNull();
        await Assert.That(attributes!.Get(saved.Objects.Names.Intern(NoteTypeKey)).Kind)
            .IsEqualTo(PdfKind.Name);
    }

    /// <summary>Changing a copied option distinguishes an explicit None subtype without mutating the defaults.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopiedOptionsKeepOptionalAbsence()
    {
        var explicitNone = PdfTagOptions.Default with { NoteType = PdfNoteType.None };
        await Assert.That(PdfTagOptions.Default.NoteType).IsNull();
        await Assert.That(explicitNone.NoteType).IsEqualTo(PdfNoteType.None);
        await Assert.That(explicitNone).IsNotEqualTo(PdfTagOptions.Default);
    }

    /// <summary>Positional construction retains all fields and supports record deconstruction.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PositionalOptionsRetainTheirFields()
    {
        var options = new PdfTagOptions("description", "replacement", "en", "title", PdfTableScope.None, 0, 0, PdfNoteType.Endnote);
        var (description, replacement, language, title, scope, rows, columns, type) = options;
        await Assert.That(description).IsEqualTo(options.AlternativeText);
        await Assert.That(replacement).IsEqualTo(options.ActualText);
        await Assert.That(language).IsEqualTo(options.Language);
        await Assert.That(title).IsEqualTo(options.Title);
        await Assert.That(scope).IsEqualTo(options.TableScope);
        await Assert.That(rows).IsEqualTo(options.RowSpan);
        await Assert.That(columns).IsEqualTo(options.ColumnSpan);
        await Assert.That(type).IsEqualTo(PdfNoteType.Endnote);
    }

    /// <summary>Checks that veraPDF accepts every generated subtype and its omission, and rejects a nonstandard name.</summary>
    /// <param name="flavour">The independent PDF/UA-2 or WTPDF profile.</param>
    /// <param name="profileName">The expected profile title.</param>
    /// <param name="cancellationToken">Cancels writing and independent validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("ua2", "PDF/UA-2 + Tagged PDF validation profile")]
    [Arguments("wt1r", "WTPDF 1.0 Reuse validation profile")]
    [Arguments("wt1a", "WTPDF 1.0 Accessibility validation profile")]
    [NotInParallel(nameof(VeraPdfValidator))]
    public async Task GeneratedSubtypesAgreeWithVeraPdf(string flavour, string profileName, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("note-subtypes");
        PdfNoteType?[] types = [null, PdfNoteType.None, PdfNoteType.Footnote, PdfNoteType.Endnote];
        var files = new List<string>();
        foreach (var type in types)
        {
            var path = Path.Combine(folder.Path, $"{type?.ToString() ?? "omitted"}.pdf");
            await File.WriteAllBytesAsync(path, CreateNoteBytes(new() { NoteType = type }), cancellationToken);
            files.Add(path);
        }

        var control = Path.Combine(folder.Path, "invalid-subtype.pdf");
        using (var document = await PdfDocumentReader.OpenAsync(
            CreateNoteBytes(new() { NoteType = PdfNoteType.Footnote }),
            null,
            cancellationToken))
        {
            var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
            var edited = note.Dictionary.Clone();
            var attributes = edited.GetDictionary(KnownName.A)!.Clone();
            attributes.Set(document.Objects.Names.Intern(NoteTypeKey), PdfValue.FromName(document.Objects.Names.Intern("UnknownSubtype")));
            edited.Set(KnownName.A, PdfValue.FromDictionary(attributes));
            StoreEditing.Replace(document.Objects, note.Id, PdfValue.FromDictionary(edited));
            await using var output = File.Create(control);

            // This unclaimed control must retain the defect so the independent rule demonstrates its detection.
            await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, cancellationToken);
        }

        files.Add(control);
        var jobs = await VeraPdfValidator.ValidateAsync(home, flavour, files, cancellationToken);
        foreach (var path in files)
        {
            var job = jobs[Path.GetFullPath(path)];
            await Assert.That(job.ProfileName).IsEqualTo(profileName);
            var subtypeFailed = job.Failures.Exists(static failure =>
                failure.Clause == NoteClause && failure.TestNumber == SubtypeRule);
            await Assert.That(subtypeFailed).IsEqualTo(path == control).Because(job.FailureSummary);
        }
    }

    /// <summary>Creates a tagged file with a blank page and one note.</summary>
    /// <param name="options">The requested note attributes.</param>
    /// <returns>The serialized file.</returns>
    private static byte[] CreateNoteBytes(PdfTagOptions options)
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        _ = creator.Tags!.AddElement(creator.Tags.Root, FootnoteType, options);
        var page = creator.BeginPage(PageSide, PageSide);
        try
        {
            _ = page.Finish();
        }
        finally
        {
            page.Dispose();
        }

        return creator.ToArray();
    }
}
