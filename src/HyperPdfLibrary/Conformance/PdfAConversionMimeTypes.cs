// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace HyperPdfLibrary.Conformance;

/// <summary>Checks and guesses the MIME type of an embedded file (ISO 19005-3 §6.8).</summary>
internal static class PdfAConversionMimeTypes
{
    /// <summary>The type used when the file name gives no clue.</summary>
    private const string Unknown = "application/octet-stream";

    /// <summary>The MIME types of common file name extensions.</summary>
    private static readonly FrozenDictionary<string, string> ByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".xml"] = "application/xml",
        [".json"] = "application/json",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".htm"] = "text/html",
        [".html"] = "text/html",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".zip"] = "application/zip",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".odt"] = "application/vnd.oasis.opendocument.text",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Determines whether a /Subtype names a MIME type: a type and a subtype of word characters, dots, plus and minus signs.</summary>
    /// <param name="subtype">The /Subtype name, or empty.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool IsValid(string subtype)
    {
        var slash = subtype.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 && slash < subtype.Length - 1 && IsToken(subtype.AsSpan(0, slash)) && IsToken(subtype.AsSpan(slash + 1));
    }

    /// <summary>Guesses a file's MIME type from its name.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The type.</returns>
    internal static string Guess(string name) => ByExtension.TryGetValue(Path.GetExtension(name), out var type) ? type : Unknown;

    /// <summary>Determines whether text is one MIME token.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when every character is a letter, digit, underscore, dot, plus or minus.</returns>
    private static bool IsToken(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '+' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
