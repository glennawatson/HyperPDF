// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Reads the attributes of a form field, which a field inherits from its <c>/Parent</c> chain.</summary>
internal static class FieldAttributes
{
    /// <summary>The most <c>/Parent</c> steps followed.</summary>
    private const int MaxDepth = 32;

    /// <summary>Gets the bytes of the name "Off".</summary>
    private static ReadOnlySpan<byte> OffSpelling => "Off"u8;

    /// <summary>Finds an inheritable attribute, starting at a dictionary and climbing its parents.</summary>
    /// <param name="start">The first dictionary to look in.</param>
    /// <param name="key">The attribute's key.</param>
    /// <returns>The resolved value; null when no dictionary has it.</returns>
    internal static PdfValue Find(PdfDictionary start, PdfName key)
    {
        var node = start;
        for (var depth = 0; node is not null && depth < MaxDepth; depth++)
        {
            if (node.ContainsKey(key))
            {
                return node.Get(key);
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return default;
    }

    /// <summary>Gets the flags of a field.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The inherited <c>/Ff</c> flags.</returns>
    internal static PdfFieldFlags GetFlags(PdfDictionary field) => (PdfFieldFlags)(uint)Find(field, KnownName.Ff).AsInteger();

    /// <summary>Builds a field's full name: the partial names of its parents and itself joined by dots.</summary>
    /// <param name="field">The field or widget dictionary.</param>
    /// <returns>The name; empty when no level has one.</returns>
    internal static string GetFullName(PdfDictionary field)
    {
        var name = string.Empty;
        var node = field;
        for (var depth = 0; node is not null && depth < MaxDepth; depth++)
        {
            var part = node.GetText(KnownName.T);
            if (!string.IsNullOrEmpty(part))
            {
                name = name.Length == 0 ? part : $"{part}.{name}";
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return name;
    }

    /// <summary>Determines whether a field or any of its parents has a partial name, without building the name.</summary>
    /// <param name="field">The field or widget dictionary.</param>
    /// <returns><see langword="true"/> when some level has a partial name that is not empty.</returns>
    internal static bool HasName(PdfDictionary field)
    {
        var node = field;
        for (var depth = 0; node is not null && depth < MaxDepth; depth++)
        {
            var part = node.GetStringBytes(KnownName.T);
            if (!part.IsEmpty && PdfText.GetDecodedLength(part) > 0)
            {
                return true;
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return false;
    }

    /// <summary>Reads a value as text the way form fields hold it: a string, or a stream of text.</summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>The text; empty for other kinds.</returns>
    internal static string ReadText(PdfValue value) => value.Kind switch
    {
        PdfKind.String => PdfText.Decode(value.AsStringBytes()),
        PdfKind.Stream => PdfText.Decode(value.AsStream()!.DecodeToArray()),
        _ => string.Empty,
    };

    /// <summary>Reads a value as a name or string's bytes decoded as text, as an appearance state is held.</summary>
    /// <param name="value">The resolved value.</param>
    /// <param name="names">The document's names.</param>
    /// <returns>The state's spelling; empty for other kinds.</returns>
    internal static string ReadState(PdfValue value, PdfNameTable names) => value.Kind switch
    {
        PdfKind.Name => names.GetString(value.AsName()),
        PdfKind.String => Encoding.Latin1.GetString(value.AsStringBytes()),
        _ => string.Empty,
    };

    /// <summary>Finds the on-state of a check box or radio button widget: the first key of <c>/AP /N</c> other than "Off".</summary>
    /// <param name="widget">The widget dictionary.</param>
    /// <param name="names">The document's names.</param>
    /// <returns>The state's name; empty when the widget has none.</returns>
    internal static string GetOnState(PdfDictionary widget, PdfNameTable names)
    {
        var state = GetOnStateName(widget, names);
        return state.IsNone ? string.Empty : names.GetString(state);
    }

    /// <summary>Finds the on-state of a check box or radio button widget without making a string.</summary>
    /// <param name="widget">The widget dictionary.</param>
    /// <param name="names">The document's names.</param>
    /// <returns>The state's name; none when the widget has none.</returns>
    internal static PdfName GetOnStateName(PdfDictionary widget, PdfNameTable names)
    {
        if (widget.GetDictionary(KnownName.AP)?.GetDictionary(KnownName.N) is not { } normal)
        {
            return default;
        }

        // Readers hold the dictionary sorted by key, so the first key in that order is the on-state.
        var best = default(PdfName);
        for (var i = 0; i < normal.Count; i++)
        {
            var key = normal.GetKeyAt(i);
            var spelling = names.GetSpelling(key);
            if (spelling.SequenceEqual(OffSpelling))
            {
                continue;
            }

            if (best.IsNone || spelling.SequenceCompareTo(names.GetSpelling(best)) < 0)
            {
                best = key;
            }
        }

        return best;
    }
}
