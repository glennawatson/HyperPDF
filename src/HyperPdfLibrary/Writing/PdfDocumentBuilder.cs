// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Builds a new, standalone PDF from objects added to it and writes it straight through <see cref="PdfObjectWriter"/>
/// with a classic cross-reference table. Object 1 is the catalog and object 2 the page tree; both are written on save.
/// </summary>
/// <remarks>Not thread-safe. Unfiltered streams other than metadata are Flate-compressed when that is smaller.</remarks>
[DebuggerDisplay("PdfDocumentBuilder: {PageCount} pages, {ObjectCount} objects")]
public sealed class PdfDocumentBuilder
{
    /// <summary>The catalog's object number.</summary>
    private const int CatalogNumber = 1;

    /// <summary>The page tree's object number.</summary>
    private const int PagesNumber = 2;

    /// <summary>The prefix of the names a sheet gives the forms it draws.</summary>
    private const string FormNamePrefix = "X";

    /// <summary>The objects, where index <c>n - 1</c> holds object <c>n</c>.</summary>
    private readonly List<PdfValue> _objects = [];

    /// <summary>The pages, in order.</summary>
    private readonly List<PdfObjectId> _pages = [];

    /// <summary>Initializes a new instance of the <see cref="PdfDocumentBuilder"/> class.</summary>
    public PdfDocumentBuilder()
    {
        Names = new();
        _objects.Add(default);
        _objects.Add(default);
    }

    /// <summary>Gets the page tree's id, the <c>/Parent</c> of every page.</summary>
    public static PdfObjectId PagesId => new(PagesNumber, 0);

    /// <summary>Gets the name table the document's names come from.</summary>
    public PdfNameTable Names { get; }

    /// <summary>Gets the number of pages added.</summary>
    public int PageCount => _pages.Count;

    /// <summary>Gets the number of objects, including the catalog and page tree.</summary>
    public int ObjectCount => _objects.Count;

    /// <summary>Gets or sets the catalog entries besides /Type and /Pages, such as /AcroForm, /Outlines and /PageLabels.</summary>
    internal PdfDictionary CatalogEntries { get; set; } = new(null);

    /// <summary>Gets or sets the header version, such as 1.7 or 2.0.</summary>
    internal PdfHeaderVersion Version { get; set; } = PdfHeaderVersion.Pdf17;

    /// <summary>Reserves an object number to fill in later with <see cref="Set"/>, so objects can refer to each other.</summary>
    /// <returns>The new object's id.</returns>
    public PdfObjectId Reserve()
    {
        _objects.Add(default);
        return new(_objects.Count, 0);
    }

    /// <summary>Adds an object.</summary>
    /// <param name="value">The value; a stream is written with its data.</param>
    /// <returns>The new object's id.</returns>
    public PdfObjectId Add(PdfValue value)
    {
        _objects.Add(value);
        return new(_objects.Count, 0);
    }

    /// <summary>Sets the value of an object.</summary>
    /// <param name="id">The object id, from <see cref="Reserve"/> or <see cref="Add"/>.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is not an object of this document, or is the catalog or page tree.</exception>
    public void Set(PdfObjectId id, PdfValue value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(id.Number, PagesNumber + 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id.Number, _objects.Count);
        _objects[id.Number - 1] = value;
    }

    /// <summary>Adds a page dictionary as the next page; its /Type and /Parent are set.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <returns>The page's id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfObjectId AddPage(PdfDictionary page) => AddPage(Reserve(), page);

    /// <summary>Adds a page dictionary as the next page, under an id reserved earlier.</summary>
    /// <param name="id">The reserved id.</param>
    /// <param name="page">The page dictionary.</param>
    /// <returns>The page's id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is not an object of this document.</exception>
    public PdfObjectId AddPage(PdfObjectId id, PdfDictionary page)
    {
        ArgumentNullException.ThrowIfNull(page);
        page.Set(KnownName.Type, PdfValue.FromName(KnownName.Page));
        page.Set(KnownName.Parent, PdfValue.FromReference(PagesId));
        Set(id, PdfValue.FromDictionary(page));
        _pages.Add(id);
        return id;
    }

    /// <summary>Adds an upright page that draws forms through a transform each.</summary>
    /// <param name="width">The page width in points.</param>
    /// <param name="height">The page height in points.</param>
    /// <param name="placements">The forms, drawn in order.</param>
    /// <returns>The page's id.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfObjectId AddSheet(float width, float height, ReadOnlySpan<PdfFormPlacement> placements) =>
        AddSheet(width, height, 0, placements);

    /// <summary>Adds a page that draws forms through a transform each.</summary>
    /// <param name="width">The media box width in points.</param>
    /// <param name="height">The media box height in points.</param>
    /// <param name="rotate">The page's /Rotate: 0, 90, 180 or 270.</param>
    /// <param name="placements">The forms, drawn in order.</param>
    /// <returns>The page's id.</returns>
    public PdfObjectId AddSheet(float width, float height, int rotate, ReadOnlySpan<PdfFormPlacement> placements)
    {
        var forms = new PdfDictionary(null);
        var content = default(PooledBuffer);
        try
        {
            for (var i = 0; i < placements.Length; i++)
            {
                var name = Names.Intern(string.Create(CultureInfo.InvariantCulture, $"{FormNamePrefix}{i}"));
                forms.Set(name, PdfValue.FromReference(placements[i].Form));
                WriteDraw(ref content, placements[i].Transform, name);
            }

            var resources = new PdfDictionary(null);
            resources.Set(KnownName.XObject, PdfValue.FromDictionary(forms));
            var page = new PdfDictionary(null);
            page.Set(KnownName.MediaBox, PdfValue.FromArray(PdfArray.FromNumbers(null, [0, 0, width, height])));
            if (rotate != 0)
            {
                page.Set(KnownName.Rotate, PdfValue.FromInteger(rotate));
            }

            page.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
            var stream = new PdfStream(new(null), content.ToArray());
            page.Set(KnownName.Contents, PdfValue.FromReference(Add(PdfValue.FromStream(stream))));
            return AddPage(page);
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>Writes the document to a stream.</summary>
    /// <param name="destination">The stream.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">A value cannot be written.</exception>
    public void Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var writer = new PdfObjectWriter(Names);
        try
        {
            Write(ref writer);
            destination.Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes the document into a new array.</summary>
    /// <returns>The file bytes.</returns>
    /// <exception cref="PdfException">A value cannot be written.</exception>
    public byte[] ToArray()
    {
        var writer = new PdfObjectWriter(Names);
        try
        {
            Write(ref writer);
            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes a number and a space.</summary>
    /// <param name="content">The content being built.</param>
    /// <param name="number">The number.</param>
    private static void WriteNumberAndSpace(ref PooledBuffer content, float number)
    {
        PdfSyntax.WriteNumber(ref content, number);
        content.WriteByte((byte)' ');
    }

    /// <summary>Determines whether a stream has no filter, so compressing it is lossless.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns><see langword="true"/> when it has neither /Filter nor /DecodeParms.</returns>
    private static bool IsUnfiltered(PdfDictionary dictionary) =>
        dictionary.GetRaw(KnownName.Filter).IsNull && dictionary.GetRaw(KnownName.DecodeParms).IsNull;

    /// <summary>Writes an unfiltered stream, Flate-compressed when that is smaller.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="stream">The stream.</param>
    private static void WriteCompressed(ref PdfObjectWriter writer, PdfStream stream)
    {
        using var raw = stream.LeaseRawData();
        var data = PdfObjectWriter.PlainData(stream, raw.Span);
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(data, ref compressed);
            var smaller = compressed.Length < data.Length;
            writer.WriteStream(stream.Dictionary, smaller ? compressed.WrittenSpan : data, default, smaller);
        }
        finally
        {
            compressed.Dispose();
        }
    }

    /// <summary>Writes <c>q a b c d e f cm /name Do Q</c>.</summary>
    /// <param name="content">The content being built.</param>
    /// <param name="transform">The transform.</param>
    /// <param name="name">The form's resource name.</param>
    private void WriteDraw(ref PooledBuffer content, Matrix3x2 transform, PdfName name)
    {
        content.Write("q "u8);
        WriteNumberAndSpace(ref content, transform.M11);
        WriteNumberAndSpace(ref content, transform.M12);
        WriteNumberAndSpace(ref content, transform.M21);
        WriteNumberAndSpace(ref content, transform.M22);
        WriteNumberAndSpace(ref content, transform.M31);
        WriteNumberAndSpace(ref content, transform.M32);
        content.Write("cm "u8);
        PdfSyntax.WriteName(ref content, Names.GetSpelling(name));
        content.Write(" Do Q\n"u8);
    }

    /// <summary>
    /// Writes one object under its own number. Metadata streams stay plain, as XMP readers expect them and PDF/A-1 forbids
    /// a filtered metadata stream (ISO 19005-1 §6.7.2, Cor.2).
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="number">The object number.</param>
    private void WriteObject(ref PdfObjectWriter writer, int number)
    {
        var id = new PdfObjectId(number, 0);
        var value = _objects[number - 1];
        if (value.AsStream() is { } stream && IsUnfiltered(stream.Dictionary) && !stream.Dictionary.IsName(KnownName.Type, KnownName.Metadata))
        {
            writer.WriteObjectHeader(id);
            WriteCompressed(ref writer, stream);
            writer.WriteRaw("\nendobj\n"u8);
            return;
        }

        writer.WriteIndirectObject(id, value, false);
    }

    /// <summary>Fills in the catalog and the page tree.</summary>
    private void SetRoots()
    {
        var catalog = new PdfDictionary(null);
        for (var i = 0; i < CatalogEntries.Count; i++)
        {
            catalog.Set(CatalogEntries.GetKeyAt(i), CatalogEntries.GetValueAt(i));
        }

        catalog.Set(KnownName.Type, PdfValue.FromName(KnownName.Catalog));
        catalog.Set(KnownName.Pages, PdfValue.FromReference(PagesId));
        _objects[CatalogNumber - 1] = PdfValue.FromDictionary(catalog);

        var kids = new PdfArray(null, _pages.Count);
        foreach (var page in _pages)
        {
            kids.Add(PdfValue.FromReference(page));
        }

        var pages = new PdfDictionary(null);
        pages.Set(KnownName.Type, PdfValue.FromName(KnownName.Pages));
        pages.Set(KnownName.Kids, PdfValue.FromArray(kids));
        pages.Set(KnownName.Count, PdfValue.FromInteger(_pages.Count));
        _objects[PagesNumber - 1] = PdfValue.FromDictionary(pages);
    }

    /// <summary>Writes the whole file.</summary>
    /// <param name="writer">The writer.</param>
    private void Write(ref PdfObjectWriter writer)
    {
        SetRoots();
        writer.WriteRaw(Version switch
        {
            PdfHeaderVersion.Pdf14 => "%PDF-1.4\n"u8,
            PdfHeaderVersion.Pdf20 => "%PDF-2.0\n"u8,
            _ => "%PDF-1.7\n"u8,
        });
        writer.WriteRaw(PdfXrefWriter.BinaryMarker);
        var rows = new XrefRow[_objects.Count + 1];
        rows[0] = XrefRow.FreeHead;
        for (var number = 1; number <= _objects.Count; number++)
        {
            rows[number] = new(number, XrefEntryType.InFile, writer.Length, 0);
            WriteObject(ref writer, number);
        }

        var trailer = new PdfDictionary(null);
        trailer.Set(KnownName.Size, PdfValue.FromInteger(rows.Length));
        trailer.Set(KnownName.Root, PdfValue.FromReference(new(CatalogNumber, 0)));
        PdfXrefWriter.SetFileId(trailer, new(null), writer.WrittenSpan, false);
        var tableOffset = writer.Length;
        PdfXrefWriter.WriteTable(ref writer, rows);
        PdfXrefWriter.WriteTrailer(ref writer, trailer, tableOffset);
    }
}
