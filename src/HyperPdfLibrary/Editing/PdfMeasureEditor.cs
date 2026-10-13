// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Writes a page's viewports (<c>/VP</c>, ISO 32000-2 12.9.2, Table 262) with rectilinear measure dictionaries
/// (12.9.2, Table 266) and their number formats (12.9.2, Table 267). Geospatial measures are read-only.
/// </summary>
internal static class PdfMeasureEditor
{
    /// <summary>The numbers in a viewport box.</summary>
    private const int BoxNumbers = 4;

    /// <summary>The first version with viewports and measure dictionaries (12.9.2).</summary>
    private static readonly PdfVersion ViewportVersion = new(1, 6);

    /// <summary>The number formats the standard allows (Table 267, <c>/F</c>): decimal, fraction, round and truncate.</summary>
    private static readonly string[] Formats = ["D", "F", "R", "T"];

    /// <summary>Sets a page's viewports; an empty list removes them.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="pageId">The page's object.</param>
    /// <param name="viewports">The viewports.</param>
    /// <exception cref="ArgumentException">A viewport has no box, or a measure is not a complete rectilinear measure.</exception>
    internal static void SetViewports(PdfObjectEdits edits, PdfObjectId pageId, IReadOnlyList<PdfViewport> viewports)
    {
        var page = edits.Edit(pageId) ?? throw new ArgumentException("The page is not a dictionary.", nameof(pageId));
        var store = edits.Store;
        var key = store.Names.Intern("VP"u8);
        _ = page.Remove(key);
        if (viewports.Count == 0)
        {
            return;
        }

        var list = new PdfArray(store, viewports.Count);
        foreach (var viewport in viewports)
        {
            list.Add(PdfValue.FromDictionary(BuildViewport(store, viewport)));
        }

        page.Set(key, PdfValue.FromArray(list));
        PdfCatalogEditor.RaiseVersion(edits, ViewportVersion);
    }

    /// <summary>Builds a viewport dictionary.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="viewport">The viewport.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary BuildViewport(PdfObjectStore store, PdfViewport viewport)
    {
        var result = new PdfDictionary(store);
        result.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("Viewport"u8)));
        result.Set(KnownName.BBox, PdfValue.FromArray(BuildBox(store, viewport)));
        if (!string.IsNullOrEmpty(viewport.Name))
        {
            result.Set(KnownName.Name, PdfValue.FromString(PdfText.Encode(viewport.Name, store.Version)));
        }

        if (viewport.Measure is { } measure)
        {
            result.Set(KnownName.Measure, PdfValue.FromDictionary(BuildMeasure(store, measure)));
        }

        return result;
    }

    /// <summary>Builds the box, in the order written when it is known, which a geospatial map reads.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="viewport">The viewport.</param>
    /// <returns>The four numbers.</returns>
    /// <exception cref="ArgumentException">The viewport has no non-empty box.</exception>
    private static PdfArray BuildBox(PdfObjectStore store, PdfViewport viewport)
    {
        if (viewport.BoundingBox.Length == BoxNumbers)
        {
            var array = new PdfArray(store, BoxNumbers);
            foreach (var number in viewport.BoundingBox)
            {
                array.Add(PdfValue.FromReal(number));
            }

            return array;
        }

        return viewport.Bounds is { IsEmpty: false } bounds ? bounds.ToArray(store) : throw new ArgumentException("A viewport needs a box with an area.", nameof(viewport));
    }

    /// <summary>Builds a rectilinear measure dictionary.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="measure">The measure.</param>
    /// <returns>The dictionary.</returns>
    /// <exception cref="ArgumentException">The measure is geospatial, or lacks the scale text or a required format list.</exception>
    private static PdfDictionary BuildMeasure(PdfObjectStore store, PdfMeasure measure)
    {
        if (!string.Equals(measure.Subtype, "RL", StringComparison.Ordinal) || measure.Geo is not null)
        {
            throw new ArgumentException("Only rectilinear measures can be written; geospatial measures are read-only.", nameof(measure));
        }

        if (string.IsNullOrEmpty(measure.Ratio) || measure.X.Length == 0 || measure.Distance.Length == 0 || measure.Area.Length == 0)
        {
            throw new ArgumentException("A measure needs a scale ratio and number formats for x, distance and area.", nameof(measure));
        }

        var result = new PdfDictionary(store);
        result.Set(KnownName.Type, PdfValue.FromName(KnownName.Measure));
        result.Set(KnownName.Subtype, PdfValue.FromName(store.Names.Intern("RL"u8)));
        result.Set(KnownName.R, PdfValue.FromString(PdfText.Encode(measure.Ratio, store.Version)));
        SetFormats(store, result, "X"u8, measure.X);
        SetFormats(store, result, "Y"u8, measure.Y);
        SetFormats(store, result, "D"u8, measure.Distance);
        SetFormats(store, result, "A"u8, measure.Area);
        SetFormats(store, result, "T"u8, measure.Angle);
        SetFormats(store, result, "S"u8, measure.Slope);
        if (measure.Origin.Length > 0)
        {
            result.Set(store.Names.Intern("O"u8), PdfValue.FromArray(Numbers(store, measure.Origin)));
        }

        if (measure.Y.Length > 0)
        {
            result.Set(store.Names.Intern("CYX"u8), PdfValue.FromReal(measure.YToXRatio));
        }

        return result;
    }

    /// <summary>Writes an array of number format dictionaries when there are any.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="measure">The measure dictionary.</param>
    /// <param name="key">The key as UTF-8.</param>
    /// <param name="formats">The formats.</param>
    private static void SetFormats(PdfObjectStore store, PdfDictionary measure, ReadOnlySpan<byte> key, PdfNumberFormat[] formats)
    {
        if (formats.Length == 0)
        {
            return;
        }

        var array = new PdfArray(store, formats.Length);
        foreach (var format in formats)
        {
            array.Add(PdfValue.FromDictionary(BuildFormat(store, format)));
        }

        measure.Set(store.Names.Intern(key), PdfValue.FromArray(array));
    }

    /// <summary>Builds a number format dictionary (Table 267).</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="format">The format.</param>
    /// <returns>The dictionary.</returns>
    /// <exception cref="ArgumentException">The unit is empty, the conversion is not positive, or the format letter is unknown.</exception>
    private static PdfDictionary BuildFormat(PdfObjectStore store, PdfNumberFormat format)
    {
        if (string.IsNullOrEmpty(format.Unit) || !(format.Conversion > 0) || Array.IndexOf(Formats, format.Format) < 0)
        {
            throw new ArgumentException("A number format needs a unit, a conversion above zero and a format of D, F, R or T.", nameof(format));
        }

        var result = new PdfDictionary(store);
        result.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("NumberFormat"u8)));
        result.Set(store.Names.Intern("U"u8), Text(store, format.Unit));
        result.Set(KnownName.C, PdfValue.FromReal(format.Conversion));
        result.Set(KnownName.F, PdfValue.FromName(store.Names.Intern(format.Format)));
        result.Set(KnownName.D, PdfValue.FromInteger(format.Precision));
        result.Set(store.Names.Intern("FD"u8), PdfValue.FromBoolean(format.Fractional));
        result.Set(store.Names.Intern("RT"u8), Text(store, format.ThousandsSeparator));
        result.Set(store.Names.Intern("RD"u8), Text(store, format.DecimalSeparator));
        result.Set(store.Names.Intern("PS"u8), Text(store, format.Prefix));
        result.Set(store.Names.Intern("SS"u8), Text(store, format.Suffix));
        result.Set(store.Names.Intern("O"u8), PdfValue.FromName(store.Names.Intern(format.LabelPosition)));
        return result;
    }

    /// <summary>Makes a text string value.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfValue Text(PdfObjectStore store, string text) => PdfValue.FromString(PdfText.Encode(text, store.Version));

    /// <summary>Makes an array of real numbers.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="numbers">The numbers.</param>
    /// <returns>The array.</returns>
    private static PdfArray Numbers(PdfObjectStore store, double[] numbers)
    {
        var array = new PdfArray(store, numbers.Length);
        foreach (var number in numbers)
        {
            array.Add(PdfValue.FromReal(number));
        }

        return array;
    }
}
