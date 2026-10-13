// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Turns the entries between <c>BI</c> and <c>ID</c> of an inline image into the dictionary of an image XObject
/// (ISO 32000-2 §8.9.7, Table 91 and Table 92). Inline images may abbreviate keys, colour spaces and filters, and name a
/// colour space through the page resources; an image XObject may do neither.
/// </summary>
internal static class InlineImageDictionary
{
    /// <summary>The length of the <c>&lt;&lt;</c> opening delimiter put before the entries.</summary>
    private const int OpenLength = 2;

    /// <summary>The length of the two delimiters put around the entries.</summary>
    private const int DelimitersLength = 4;

    /// <summary>The extra entries an image XObject has: /Type and /Subtype.</summary>
    private const int ExtraEntries = 2;

    /// <summary>The index of the colour space family in a colour space array.</summary>
    private const int FamilyIndex = 0;

    /// <summary>The index of the base colour space in an Indexed colour space array.</summary>
    private const int BaseIndex = 1;

    /// <summary>The abbreviated keys of an inline image dictionary (Table 91).</summary>
    private static readonly Abbreviation[] Keys =
    [
        new("BPC"u8.ToArray(), KnownName.BitsPerComponent),
        new("CS"u8.ToArray(), KnownName.ColorSpace),
        new("D"u8.ToArray(), KnownName.Decode),
        new("DP"u8.ToArray(), KnownName.DecodeParms),
        new("F"u8.ToArray(), KnownName.Filter),
        new("H"u8.ToArray(), KnownName.Height),
        new("IM"u8.ToArray(), KnownName.ImageMask),
        new("I"u8.ToArray(), KnownName.Interpolate),
        new("W"u8.ToArray(), KnownName.Width),
    ];

    /// <summary>The abbreviated filter names of an inline image (Table 94).</summary>
    private static readonly Abbreviation[] Filters =
    [
        new("AHx"u8.ToArray(), KnownName.ASCIIHexDecode),
        new("A85"u8.ToArray(), KnownName.ASCII85Decode),
        new("LZW"u8.ToArray(), KnownName.LZWDecode),
        new("Fl"u8.ToArray(), KnownName.FlateDecode),
        new("RL"u8.ToArray(), KnownName.RunLengthDecode),
        new("CCF"u8.ToArray(), KnownName.CCITTFaxDecode),
        new("DCT"u8.ToArray(), KnownName.DCTDecode),
    ];

    /// <summary>The abbreviated colour space names of an inline image (Table 93).</summary>
    private static readonly Abbreviation[] Spaces =
    [
        new("G"u8.ToArray(), KnownName.DeviceGray),
        new("RGB"u8.ToArray(), KnownName.DeviceRGB),
        new("CMYK"u8.ToArray(), KnownName.DeviceCMYK),
    ];

    /// <summary>Gets the key of the inline image length (/L, ISO 32000-2 Table 92), which has no XObject form: the stream's own /Length replaces it.</summary>
    private static ReadOnlySpan<byte> LengthKey => "L"u8;

    /// <summary>Gets the abbreviation of the Indexed colour space family.</summary>
    private static ReadOnlySpan<byte> IndexedShort => "I"u8;

    /// <summary>Builds the dictionary of the image XObject.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="entries">The text between <c>BI</c> and <c>ID</c>.</param>
    /// <param name="resources">The page's resources, to find a named colour space in.</param>
    /// <returns>The dictionary, or <see langword="null"/> when the entries cannot be read or name a colour space the resources lack.</returns>
    internal static PdfDictionary? Build(PdfObjectStore store, ReadOnlySpan<byte> entries, PageResourceEdit resources)
    {
        var text = new byte[entries.Length + DelimitersLength];
        "<<"u8.CopyTo(text);
        entries.CopyTo(text.AsSpan(OpenLength));
        ">>"u8.CopyTo(text.AsSpan(OpenLength + entries.Length));
        if (new PdfParser(text, 0, store, store.Names).ParseValue().AsDictionary() is not { } source)
        {
            return null;
        }

        var result = new PdfDictionary(store, source.Count + ExtraEntries);
        result.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        result.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Image));
        for (var i = 0; i < source.Count; i++)
        {
            if (!TryAdd(store, result, source.GetKeyAt(i), source.GetValueAt(i), resources))
            {
                return null;
            }
        }

        return result;
    }

    /// <summary>Adds one entry under its full key with its value expanded.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="result">The XObject dictionary.</param>
    /// <param name="key">The inline key.</param>
    /// <param name="value">The inline value.</param>
    /// <param name="resources">The page's resources.</param>
    /// <returns><see langword="false"/> when the value cannot be expressed in an XObject.</returns>
    private static bool TryAdd(PdfObjectStore store, PdfDictionary result, PdfName key, PdfValue value, PageResourceEdit resources)
    {
        var spelling = store.Names.GetSpelling(key);
        if (spelling.SequenceEqual(LengthKey))
        {
            return true;
        }

        var full = TryExpand(Keys, spelling, out var known) ? known : store.Names.Intern(spelling);
        PdfValue? expanded = value;
        if (full.Is(KnownName.ColorSpace))
        {
            expanded = ExpandColorSpace(store, value, resources, 0);
        }
        else if (full.Is(KnownName.Filter))
        {
            expanded = ExpandFilters(store, value);
        }

        if (expanded is not { } entry)
        {
            return false;
        }

        result.Set(full, entry);
        return true;
    }

    /// <summary>Finds the full name an abbreviation stands for.</summary>
    /// <param name="table">The abbreviations.</param>
    /// <param name="spelling">The name as written.</param>
    /// <param name="full">The full name.</param>
    /// <returns><see langword="true"/> when the name is in the table.</returns>
    private static bool TryExpand(Abbreviation[] table, ReadOnlySpan<byte> spelling, out KnownName full)
    {
        full = default;
        foreach (var (shortName, name) in table)
        {
            if (!spelling.SequenceEqual(shortName))
            {
                continue;
            }

            full = name;
            return true;
        }

        return false;
    }

    /// <summary>Expands a colour space value: abbreviations, names from the resources, and the names inside an Indexed array.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="value">The inline colour space.</param>
    /// <param name="resources">The page's resources.</param>
    /// <param name="depth">The nesting depth, to stop at the base of an Indexed space.</param>
    /// <returns>The colour space for the XObject, or <see langword="null"/> when a named space is not in the resources.</returns>
    private static PdfValue? ExpandColorSpace(PdfObjectStore store, PdfValue value, PageResourceEdit resources, int depth)
    {
        if (value.AsArray() is { } array)
        {
            return ExpandColorSpaceArray(store, array, resources, depth);
        }

        if (value.AsName() is not { IsNone: false } name)
        {
            return value;
        }

        if (TryExpand(Spaces, store.Names.GetSpelling(name), out var device))
        {
            return PdfValue.FromName(device);
        }

        // The device spaces and Pattern are named as they are; anything else is a name in the page resources.
        if (name.ToKnownName() is KnownName.DeviceGray or KnownName.DeviceRGB or KnownName.DeviceCMYK or KnownName.Pattern)
        {
            return value;
        }

        var found = resources.GetColorSpace(name);
        return found.IsNull ? null : found;
    }

    /// <summary>Expands the names in a colour space array.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="array">The inline array.</param>
    /// <param name="resources">The page's resources.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The array, or <see langword="null"/> when the base space cannot be found.</returns>
    private static PdfValue? ExpandColorSpaceArray(PdfObjectStore store, PdfArray array, PageResourceEdit resources, int depth)
    {
        var result = new PdfArray(store, array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            var item = array.GetRaw(i);
            if (i == FamilyIndex && item.AsName() is { IsNone: false } family && store.Names.GetSpelling(family).SequenceEqual(IndexedShort))
            {
                item = PdfValue.FromName(KnownName.Indexed);
            }
            else if (i == BaseIndex && depth == 0 && item.AsName() is { IsNone: false })
            {
                // The base space of an Indexed array may be abbreviated or named in the resources.
                if (ExpandColorSpace(store, item, resources, depth + 1) is not { } expanded)
                {
                    return null;
                }

                item = expanded;
            }

            result.Add(item);
        }

        return PdfValue.FromArray(result);
    }

    /// <summary>Expands filter abbreviations in a name or an array of names.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="value">The inline filter value.</param>
    /// <returns>The value for the XObject.</returns>
    private static PdfValue ExpandFilters(PdfObjectStore store, PdfValue value)
    {
        if (value.AsArray() is not { } array)
        {
            return ExpandFilter(store, value);
        }

        var result = new PdfArray(store, array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            result.Add(ExpandFilter(store, array.GetRaw(i)));
        }

        return PdfValue.FromArray(result);
    }

    /// <summary>Expands one filter name.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="value">The filter, usually a name.</param>
    /// <returns>The value with an abbreviated name made whole.</returns>
    private static PdfValue ExpandFilter(PdfObjectStore store, PdfValue value) =>
        value.AsName() is { IsNone: false } name && TryExpand(Filters, store.Names.GetSpelling(name), out var full) ? PdfValue.FromName(full) : value;

    /// <summary>An abbreviation and the name it stands for.</summary>
    /// <param name="Short">The abbreviation's bytes.</param>
    /// <param name="Full">The full name.</param>
    private readonly record struct Abbreviation(byte[] Short, KnownName Full);
}
