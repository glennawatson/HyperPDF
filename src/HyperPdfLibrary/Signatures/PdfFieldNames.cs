// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Reads form field names and inherited field types by walking /Parent.</summary>
internal static class PdfFieldNames
{
    /// <summary>The deepest field tree walked.</summary>
    private const int MaxDepth = 32;

    /// <summary>The most parents visited, so a loop of parents without names cannot run on without end.</summary>
    private const int MaxVisited = 1024;

    /// <summary>Gets a field's fully qualified name: the partial names (/T) of it and its parents, joined with dots.</summary>
    /// <param name="field">The field or widget dictionary.</param>
    /// <returns>The name, or an empty string when no level has a /T.</returns>
    internal static string FullName(PdfDictionary field)
    {
        // The name is measured first and then written into the string itself, so no part is decoded to a string of its own.
        var length = 0;
        var parts = 0;
        var visited = 0;
        for (var node = field; node is not null && parts < MaxDepth && visited < MaxVisited; node = node.GetDictionary(KnownName.Parent))
        {
            visited++;
            var partial = PdfText.GetDecodedLength(node.GetStringBytes(KnownName.T));
            if (partial == 0)
            {
                continue;
            }

            length += partial;
            parts++;
        }

        return parts == 0 ? string.Empty : string.Create(length + parts - 1, field, static (chars, first) => WriteName(first, chars));
    }

    /// <summary>Gets a field's type (/FT), inherited from its parents.</summary>
    /// <param name="field">The field or widget dictionary.</param>
    /// <returns>The type, or no name.</returns>
    internal static PdfName FieldType(PdfDictionary field)
    {
        var depth = 0;
        for (var node = field; node is not null && depth < MaxDepth; node = node.GetDictionary(KnownName.Parent))
        {
            var type = node.GetName(KnownName.FT);
            if (!type.IsNone)
            {
                return type;
            }

            depth++;
        }

        return default;
    }

    /// <summary>Writes a field's full name, working backwards from the field to its root.</summary>
    /// <param name="field">The field or widget dictionary.</param>
    /// <param name="chars">The string's characters, sized to hold the name.</param>
    private static void WriteName(PdfDictionary field, Span<char> chars)
    {
        var end = chars.Length;
        var parts = 0;
        var visited = 0;
        for (var node = field; node is not null && parts < MaxDepth && visited < MaxVisited; node = node.GetDictionary(KnownName.Parent))
        {
            visited++;
            var bytes = node.GetStringBytes(KnownName.T);
            var partial = PdfText.GetDecodedLength(bytes);
            if (partial == 0)
            {
                continue;
            }

            if (parts > 0)
            {
                end--;
                chars[end] = '.';
            }

            end -= partial;
            _ = PdfText.Decode(bytes, chars.Slice(end, partial));
            parts++;
        }
    }
}
