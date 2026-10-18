// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Checks and guesses the MIME type of an embedded file (ISO 19005-3 §6.8).</summary>
internal static class PdfAConversionMimeTypes
{
    /// <summary>The type used when the file name gives no clue.</summary>
    private const string Unknown = "application/octet-stream";

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
    internal static string Guess(string name)
    {
        var extension = Path.GetExtension(name.AsSpan());
        return extension.IsEmpty ? Unknown : GuessText(extension) ?? GuessImage(extension) ?? GuessDocument(extension) ?? Unknown;
    }

    /// <summary>Matches common document and text formats without materialising the extension.</summary>
    /// <param name="extension">The filename suffix.</param>
    /// <returns>The media type, or null when this group has no match.</returns>
    private static string? GuessText(ReadOnlySpan<char> extension) => extension switch
    {
        var value when value.Equals(".pdf", StringComparison.OrdinalIgnoreCase) => "application/pdf",
        var value when value.Equals(".xml", StringComparison.OrdinalIgnoreCase) => "application/xml",
        var value when value.Equals(".json", StringComparison.OrdinalIgnoreCase) => "application/json",
        var value when value.Equals(".txt", StringComparison.OrdinalIgnoreCase) => "text/plain",
        var value when value.Equals(".csv", StringComparison.OrdinalIgnoreCase) => "text/csv",
        var value when value.Equals(".htm", StringComparison.OrdinalIgnoreCase) || value.Equals(".html", StringComparison.OrdinalIgnoreCase) => "text/html",
        _ => null,
    };

    /// <summary>Matches common raster image formats.</summary>
    /// <param name="extension">The filename suffix.</param>
    /// <returns>The media type, or null when this group has no match.</returns>
    private static string? GuessImage(ReadOnlySpan<char> extension) => extension switch
    {
        var value when value.Equals(".png", StringComparison.OrdinalIgnoreCase) => "image/png",
        var value when value.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || value.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) => "image/jpeg",
        var value when value.Equals(".gif", StringComparison.OrdinalIgnoreCase) => "image/gif",
        var value when value.Equals(".tif", StringComparison.OrdinalIgnoreCase) || value.Equals(".tiff", StringComparison.OrdinalIgnoreCase) => "image/tiff",
        _ => null,
    };

    /// <summary>Matches archive and office document formats.</summary>
    /// <param name="extension">The filename suffix.</param>
    /// <returns>The media type, or null when this group has no match.</returns>
    private static string? GuessDocument(ReadOnlySpan<char> extension) => extension switch
    {
        var value when value.Equals(".zip", StringComparison.OrdinalIgnoreCase) => "application/zip",
        var value when value.Equals(".docx", StringComparison.OrdinalIgnoreCase) => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        var value when value.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        var value when value.Equals(".odt", StringComparison.OrdinalIgnoreCase) => "application/vnd.oasis.opendocument.text",
        _ => null,
    };

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
