// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>Writes separately owned Table, FENote, List and PrintField structure attributes.</summary>
internal static class PdfTagAttributes
{
    /// <summary>The capacity of a table attribute dictionary.</summary>
    private const int TableEntries = 10;

    /// <summary>The entries in a subtype attribute dictionary.</summary>
    private const int NoteEntries = 2;

    /// <summary>Rejects undefined attribute enum values before adding an element.</summary>
    /// <param name="options">The requested attributes.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is undefined.</exception>
    internal static void Validate(PdfTagOptions options)
    {
        if (options.TableScope is not (PdfTableScope.None or PdfTableScope.Row or PdfTableScope.Column or PdfTableScope.Both))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The table scope is undefined.");
        }

        if (options.NoteType is not (null or PdfNoteType.None or PdfNoteType.Footnote or PdfNoteType.Endnote))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The note subtype is undefined.");
        }
    }

    /// <summary>Sets A only when at least one owner has requested attributes.</summary>
    /// <param name="sink">The document's object and name owner.</param>
    /// <param name="element">The structure element dictionary.</param>
    /// <param name="options">The validated attributes.</param>
    internal static void Write(IPdfObjectSink sink, PdfDictionary element, PdfTagOptions options)
    {
        var table = CreateTable(sink, options);
        var note = options.NoteType is { } type ? CreateNote(sink, type) : null;
        var list = options.ListNumbering is { } numbering ? PdfTagListNumbering.Create(sink, numbering) : null;
        var field = options.PrintField is { } printField ? PdfTagPrintField.Create(sink, printField) : null;
        if ((table ?? note ?? list ?? field) is not { } attribute)
        {
            return;
        }

        var count = Count(table, note, list, field);
        if (count == 1)
        {
            element.Set(KnownName.A, PdfValue.FromDictionary(attribute));
        }
        else
        {
            var attributes = new PdfArray(sink.Owner, count);
            Add(attributes, table);
            Add(attributes, note);
            Add(attributes, list);
            Add(attributes, field);
            element.Set(KnownName.A, PdfValue.FromArray(attributes));
        }
    }

    /// <summary>Counts the separately requested attribute owners.</summary>
    /// <param name="table">The Table owner, or null.</param>
    /// <param name="note">The FENote owner, or null.</param>
    /// <param name="list">The List owner, or null.</param>
    /// <param name="field">The PrintField owner, or null.</param>
    /// <returns>The number of owners.</returns>
    private static int Count(PdfDictionary? table, PdfDictionary? note, PdfDictionary? list, PdfDictionary? field) =>
        (table is null ? 0 : 1) + (note is null ? 0 : 1) + (list is null ? 0 : 1) + (field is null ? 0 : 1);

    /// <summary>Appends a requested owner's dictionary without creating entries for omitted owners.</summary>
    /// <param name="attributes">The array of independent owners.</param>
    /// <param name="attribute">The owner's dictionary, or null.</param>
    private static void Add(PdfArray attributes, PdfDictionary? attribute)
    {
        if (attribute is not null)
        {
            attributes.Add(PdfValue.FromDictionary(attribute));
        }
    }

    /// <summary>Creates Table-owned attributes, or null when none are requested.</summary>
    /// <param name="sink">The document's object and name owner.</param>
    /// <param name="options">The validated attributes.</param>
    /// <returns>The attribute dictionary, or null.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The table scope is undefined.</exception>
    private static PdfDictionary? CreateTable(IPdfObjectSink sink, PdfTagOptions options)
    {
        if (options.TableScope == PdfTableScope.None && options.RowSpan <= 1 && options.ColumnSpan <= 1)
        {
            return null;
        }

        var names = sink.Names;
        var attributes = new PdfDictionary(sink.Owner, TableEntries);
        attributes.Set(KnownName.O, PdfValue.FromName(names.Intern("Table")));
        if (options.TableScope != PdfTableScope.None)
        {
            var scope = options.TableScope switch
            {
                PdfTableScope.Row => "Row",
                PdfTableScope.Column => "Column",
                PdfTableScope.Both => "Both",
                _ => throw new ArgumentOutOfRangeException(nameof(options)),
            };
            attributes.Set(names.Intern("Scope"), PdfValue.FromName(names.Intern(scope)));
        }

        if (options.RowSpan > 1)
        {
            attributes.Set(names.Intern("RowSpan"), PdfValue.FromInteger(options.RowSpan));
        }

        if (options.ColumnSpan > 1)
        {
            attributes.Set(names.Intern("ColSpan"), PdfValue.FromInteger(options.ColumnSpan));
        }

        return attributes;
    }

    /// <summary>Creates the optional subtype attribute defined in PDF/UA-2 8.2.5.14.2.</summary>
    /// <param name="sink">The document's object and name owner.</param>
    /// <param name="type">The validated subtype, including explicit None.</param>
    /// <returns>The FENote-owned attribute dictionary.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The note subtype is undefined.</exception>
    private static PdfDictionary CreateNote(IPdfObjectSink sink, PdfNoteType type)
    {
        var subtype = type switch
        {
            PdfNoteType.None => "None",
            PdfNoteType.Footnote => "Footnote",
            PdfNoteType.Endnote => "Endnote",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        var names = sink.Names;
        var attributes = new PdfDictionary(sink.Owner, NoteEntries);
        attributes.Set(KnownName.O, PdfValue.FromName(names.Intern("FENote")));
        attributes.Set(names.Intern("NoteType"), PdfValue.FromName(names.Intern(subtype)));
        return attributes;
    }
}
