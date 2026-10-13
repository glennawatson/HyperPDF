// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Builds documents for the form design tests and checks what the design operations wrote.</summary>
internal static class FormDesignDocuments
{
    /// <summary>The left edge of every test widget.</summary>
    internal const float Left = 72;

    /// <summary>The width of a test widget.</summary>
    internal const float Width = 120;

    /// <summary>The height of a test widget.</summary>
    internal const float Height = 24;

    /// <summary>The bottom edge of row zero.</summary>
    private const float Top = 700;

    /// <summary>The distance between two rows.</summary>
    private const float RowPitch = 50;

    /// <summary>The file written by a check, kept for tools when <see cref="QpdfCheck.OutputVariable"/> is set.</summary>
    private const string CheckName = "form-design";

    /// <summary>Opens a document of plain pages with no form.</summary>
    /// <param name="pages">The number of pages.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument OpenPlain(int pages) => PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(pages), null);

    /// <summary>Opens a tagged one page document that has no form.</summary>
    /// <returns>The document.</returns>
    internal static PdfDocument OpenTagged() => PdfDocumentReader.Open(TestPdf.CreateTagged(), null);

    /// <summary>Makes a placement on a page, in a row down the page.</summary>
    /// <param name="page">The page index.</param>
    /// <param name="row">The row, counted from the top.</param>
    /// <returns>The placement.</returns>
    internal static PdfWidgetPlacement Place(int page, int row) => new(page, Box(row));

    /// <summary>Makes the rectangle of a row.</summary>
    /// <param name="row">The row, counted from the top.</param>
    /// <returns>The rectangle.</returns>
    internal static PdfRectangle Box(int row)
    {
        var bottom = Top - (row * RowPitch);
        return new(Left, bottom, Left + Width, bottom + Height);
    }

    /// <summary>Makes a field definition of a kind with one widget on the first page.</summary>
    /// <param name="type">The kind of field.</param>
    /// <param name="name">The field name.</param>
    /// <param name="row">The row, counted from the top.</param>
    /// <returns>The definition.</returns>
    internal static PdfFieldDefinition Field(PdfFieldType type, string name, int row) => new(type, name, [Place(0, row)]);

    /// <summary>Makes a text field definition on the first page.</summary>
    /// <param name="name">The field name.</param>
    /// <param name="row">The row, counted from the top.</param>
    /// <returns>The definition.</returns>
    internal static PdfFieldDefinition Text(string name, int row) => Field(PdfFieldType.Text, name, row);

    /// <summary>Saves a document with its edits appended.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file bytes.</returns>
    internal static async Task<byte[]> SaveAsync(PdfDocument document)
    {
        await using var output = new MemoryStream();
        await PdfDocumentSaving.SaveAsync(document, output, CancellationToken.None);
        return output.ToArray();
    }

    /// <summary>Saves a document as a new compact file.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file bytes.</returns>
    internal static byte[] SaveCompact(PdfDocument document) => PdfCompactWriter.Save(document.Objects, new(false, false));

    /// <summary>Checks a file with qpdf when it is installed.</summary>
    /// <param name="name">A name for the file.</param>
    /// <param name="file">The file bytes.</param>
    /// <returns><see langword="true"/> when qpdf is not installed or finds no problem.</returns>
    internal static async Task<bool> PassesQpdfAsync(string name, byte[] file)
    {
        var outcome = await QpdfCheck.RunAsync($"{CheckName}-{name}", file, CancellationToken.None);
        return outcome is not { IsClean: false };
    }

    /// <summary>Lists every widget of a page the form reading reports.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page index.</param>
    /// <returns>The widgets.</returns>
    internal static List<PdfFormWidget> Widgets(PdfDocument document, int page)
    {
        var widgets = new List<PdfFormWidget>();
        PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(document), page, widgets);
        return widgets;
    }

    /// <summary>Gets a field's dictionary from the store.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The object.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary Dictionary(PdfDocument document, PdfObjectId id) => StoreReading.GetDictionary(document.Objects, id)!;

    /// <summary>Finds the references that point at objects the document does not have, among the objects reachable from the trailer.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The missing object numbers.</returns>
    internal static List<int> FindDanglingReferences(PdfDocument document)
    {
        var store = document.Objects;
        var missing = new List<int>();
        var seen = new HashSet<int>();
        var pending = new Stack<PdfValue>();
        pending.Push(PdfValue.FromDictionary(store.Trailer));
        while (pending.Count > 0)
        {
            var value = pending.Pop();
            if (value.IsReference)
            {
                VisitReference(store, value.AsReference(), seen, missing, pending);
                continue;
            }

            PushChildren(value, pending);
        }

        return missing;
    }

    /// <summary>Follows one reference while looking for missing objects.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="id">The reference.</param>
    /// <param name="seen">The object numbers visited.</param>
    /// <param name="missing">Receives a missing object number.</param>
    /// <param name="pending">Receives the object's value to visit.</param>
    private static void VisitReference(PdfObjectStore store, PdfObjectId id, HashSet<int> seen, List<int> missing, Stack<PdfValue> pending)
    {
        if (!seen.Add(id.Number))
        {
            return;
        }

        var target = StoreReading.GetObject(store, id);
        if (target.IsNull)
        {
            missing.Add(id.Number);
            return;
        }

        pending.Push(target);
    }

    /// <summary>Pushes the values held by a dictionary, stream or array.</summary>
    /// <param name="value">The container value.</param>
    /// <param name="pending">The values to visit.</param>
    private static void PushChildren(PdfValue value, Stack<PdfValue> pending)
    {
        if (value.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                pending.Push(array.GetRaw(i));
            }

            return;
        }

        var dictionary = value.AsStream()?.Dictionary ?? value.AsDictionary();
        for (var i = 0; dictionary is not null && i < dictionary.Count; i++)
        {
            pending.Push(dictionary.GetValueAt(i));
        }
    }
}
